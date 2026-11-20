using Esteira.Api.Autenticacao;
using Esteira.Api.Erros;
using Esteira.Api.Middleware;
using Esteira.Api.Paginacao;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Common.Persistence;
using Esteira.IoC;
using Esteira.IoC.Configuracoes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// O logger fica no host, não no Log.Logger estático: cada host (inclusive os de teste, em paralelo) escreve nos próprios sinks.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    // Rodando fora do Docker, o log vai pro Loki via OTLP (docs/agents/observabilidade-backup.md).
    var otlpEndpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        configuration.WriteTo.OpenTelemetry(otlp =>
        {
            otlp.Endpoint = otlpEndpoint;
            otlp.Protocol = context.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] == "grpc"
                ? OtlpProtocol.Grpc
                : OtlpProtocol.HttpProtobuf;
            otlp.ResourceAttributes = Configuracao.AtributosOtel(context.Configuration["OTEL_RESOURCE_ATTRIBUTES"]);
            otlp.ResourceAttributes["service.name"] = "esteira-api";
        });
    }
}, preserveStaticLogger: true);

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ResultadoFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Corpo ilegível ou com tipo errado sai no mesmo formato de erro da ADR-0003.
        options.InvalidModelStateResponseFactory = RequisicaoInvalida.Resposta;
    })
    .AddNewtonsoftJson(opt =>
    {
        opt.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        opt.SerializerSettings.ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy { OverrideSpecifiedNames = false }
        };
    });

// Migrations (dotnet ef) usam o owner do banco; a API em runtime, só a role de aplicação.
var connectionString = Configuracao.Obrigatoria(
    builder.Configuration,
    EF.IsDesignTime ? "ConnectionStrings:PostgresMigrations" : "ConnectionStrings:Postgres");

builder.Services.AddDbContext<EsteiraContext>(options => options.UsarPostgres(connectionString));
builder.Services.AddDbContext<EsteiraReadContext>(options => options.UsarPostgres(connectionString));

// Exception não tratada: Problem Details 500 com traceId, sem detalhe técnico (ADR-0003).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErroInesperadoHandler>();

builder.Services.AddOpenApi();

var keycloakAuthority = Configuracao.Obrigatoria(builder.Configuration, "Keycloak:Authority");
builder.Services.Configure<AutenticacaoSettings>(builder.Configuration.GetSection(AutenticacaoSettings.Secao));
Configuracao.Obrigatoria(builder.Configuration, "Keycloak:ClientId");
Configuracao.Obrigatoria(builder.Configuration, "Keycloak:ClientSecret");
Configuracao.Obrigatoria(builder.Configuration, "Autenticacao:UrlPublica");
// Timeout bem abaixo do TTL da trava de refresh (10 s): a troca nunca sobrevive à própria trava.
builder.Services.AddHttpClient<IProvedorOidc, ProvedorOidcKeycloak>(cliente => cliente.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<FluxoLogin>();
builder.Services.AddScoped<FluxoSessao>();
// Sessão BFF (ADR-0004): cookie opaco -> sessão no Redis. Sem JWT Bearer.
builder.Services
    .AddAuthentication(SessaoAuthenticationHandler.Esquema)
    .AddScheme<AuthenticationSchemeOptions, SessaoAuthenticationHandler>(SessaoAuthenticationHandler.Esquema, null);
// Sessão exigida por padrão em todo endpoint; quem não quer declara [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder(SessaoAuthenticationHandler.Esquema).RequireAuthenticatedUser().Build());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();

builder.Services.AddSessaoRedis(builder.Configuration);
builder.Services.AddMensageria(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddDependencyInjection();

// RabbitMQ entra pelo health check que o próprio MassTransit registra.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EsteiraContext>("postgres")
    .AddUrlGroup(new Uri($"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration"), "keycloak");

// O Ingress é o único que alcança o Service da API no cluster, então os X-Forwarded-* dele são confiáveis; por padrão o
// ASP.NET só aceitaria proxy em loopback e ignoraria o cabeçalho. Só esquema e IP: o host público vem de Autenticacao:UrlPublica.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? new[] { "http://localhost:4200" })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(HeadersPaginacao.Resposta));
});

var app = builder.Build();

// Atrás do Ingress o TLS termina no proxy: sem isto o pod enxerga sempre http e o IP do Ingress. Tem de ser o primeiro
// da pipeline (antes do log e do redirect para HTTPS).
if (!app.Environment.IsDevelopment())
    app.UseForwardedHeaders();

app.UseMiddleware<TempoRequisicaoMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Frontend");
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Em Development a API fica atrás do proxy do ng serve (mesma origem do BFF, ADR-0004): redirecionar para HTTPS
// jogaria o login para outra origem (perfil "https" do launchSettings, porta 7239). Fora dele vale o redirect.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapMetrics().AllowAnonymous();

await app.RunAsync();
