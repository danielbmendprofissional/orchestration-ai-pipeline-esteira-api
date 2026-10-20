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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using Quartz;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

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

var connectionString = builder.Configuration.GetConnectionString("EsteiraConnection");

builder.Services.AddDbContext<EsteiraContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<EsteiraReadContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddOpenApi();

builder.Services.Configure<GerenciadorTarefasSettings>(builder.Configuration.GetSection(GerenciadorTarefasSettings.Secao));

var autenticacao = builder.Configuration.GetSection(AutenticacaoSettings.Secao).Get<AutenticacaoSettings>() ?? new AutenticacaoSettings();
builder.Services.Configure<AutenticacaoSettings>(builder.Configuration.GetSection(AutenticacaoSettings.Secao));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = autenticacao.Authority;
        options.Audience = autenticacao.Audience;
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
            builder.Configuration.GetValue("RabbitMQ:Host", "rabbitmq"),
            builder.Configuration.GetValue("RabbitMQ:VirtualHost", "/"),
            h =>
            {
                h.Username(builder.Configuration.GetValue<string>("RabbitMQ:UserName"));
                h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password"));
            });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddDependencyInjection();

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

await app.RunAsync();
