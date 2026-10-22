using Esteira.Api.Autenticacao;
using Esteira.Api.Middleware;
using Esteira.Api.Paginacao;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Jobs;
using Esteira.Application.Settings;
using Esteira.Data.Common.Persistence;
using Esteira.IoC;
using Esteira.Messaging;
using Esteira.Messaging.Configuracao;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Esteira.Api.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using Prometheus;
using Quartz;
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

builder.Services.AddDbContext<EsteiraContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<EsteiraReadContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddOpenApi();

builder.Services.Configure<GerenciadorTarefasSettings>(builder.Configuration.GetSection(GerenciadorTarefasSettings.Secao));

var autenticacao = builder.Configuration.GetSection(AutenticacaoSettings.Secao).Get<AutenticacaoSettings>() ?? new AutenticacaoSettings();
var keycloakAuthority = Configuracao.Obrigatoria(builder.Configuration, "Keycloak:Authority");
builder.Services.Configure<AutenticacaoSettings>(builder.Configuration.GetSection(AutenticacaoSettings.Secao));
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

var ambienteMensageria = AmbienteMensageria.Resolver(
    builder.Configuration.GetValue<string>("RabbitMQ:Ambiente"),
    builder.Environment.IsProduction());

builder.Services.AddInMemoryInboxOutbox();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CriarAnaliseArtefatosSastConsumer>();
    x.AddConsumer<CriarAnaliseArtefatosDastConsumer>();
    x.AddConsumer<AtualizarAnaliseDastConsumer>();

    x.SetEndpointNameFormatter(new SufixoAmbienteEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance, ambienteMensageria));
    x.AddConfigureEndpointsCallback((context, nome, endpoint) => endpoint.UseInMemoryInboxOutbox(context));

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.MessageTopology.SetEntityNameFormatter(new SufixoAmbienteEntityNameFormatter(cfg.MessageTopology.EntityNameFormatter, ambienteMensageria));

        cfg.Host(
            Configuracao.Obrigatoria(builder.Configuration, "RabbitMQ:Host"),
            ushort.Parse(Configuracao.Obrigatoria(builder.Configuration, "RabbitMQ:Port")),
            Configuracao.Obrigatoria(builder.Configuration, "RabbitMQ:VirtualHost"),
            h =>
            {
                h.Username(Configuracao.Obrigatoria(builder.Configuration, "RabbitMQ:UserName"));
                h.Password(Configuracao.Obrigatoria(builder.Configuration, "RabbitMQ:Password"));
            });
        cfg.ConfigureEndpoints(context);
    });
});

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

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(AnaliseDastJob));
    q.AddJob<AnaliseDastJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(AnaliseDastJob)}-trigger")
        .WithCronSchedule("0 0/15 * * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

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
