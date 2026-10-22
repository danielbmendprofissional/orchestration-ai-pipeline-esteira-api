using Esteira.Api.Autenticacao;
using Esteira.Api.Middleware;
using Esteira.Api.Paginacao;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Common.Persistence;
using Esteira.IoC;
using Esteira.IoC.Configuracoes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services
    .AddControllers(options => options.Filters.Add<PaginacaoResultFilter>())
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

builder.Services.AddOpenApi();

var keycloakAuthority = Configuracao.Obrigatoria(builder.Configuration, "Keycloak:Authority");
builder.Services.Configure<AutenticacaoSettings>(builder.Configuration.GetSection(AutenticacaoSettings.Secao));
var autenticacao = builder.Configuration.GetSection(AutenticacaoSettings.Secao).Get<AutenticacaoSettings>() ?? new AutenticacaoSettings();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.Audience = Configuracao.Obrigatoria(builder.Configuration, "Keycloak:ClientId");
        options.RequireHttpsMetadata = autenticacao.RequerHttps;
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();

builder.Services.AddMensageria(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddDependencyInjection();

// RabbitMQ entra pelo health check que o próprio MassTransit registra.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EsteiraContext>("postgres")
    .AddUrlGroup(new Uri($"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration"), "keycloak");

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? new[] { "http://localhost:4200" })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(PaginacaoHttpExtensions.Header));
});

var app = builder.Build();

app.UseMiddleware<TempoRequisicaoMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Frontend");
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

await app.RunAsync();
