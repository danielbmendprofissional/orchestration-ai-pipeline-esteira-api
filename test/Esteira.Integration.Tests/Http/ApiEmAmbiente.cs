using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Http
{
    // A API de verdade num ambiente à escolha (Development x fora dele) e com porta HTTPS conhecida,
    // para exercitar UseHttpsRedirection e ForwardedHeaders como no cluster (atrás do Ingress).
    public sealed class ApiEmAmbiente(InfraestruturaFixture infra, string ambiente, string? portaHttps) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
            builder.UseSetting("Keycloak:ClientId", "esteira-api");
            builder.UseSetting("Keycloak:ClientSecret", "segredo-de-teste");
            builder.UseSetting("Autenticacao:UrlPublica", "https://esteira.exemplo");
            builder.UseSetting("Autenticacao:RequerHttps", "false");

            if (portaHttps is not null)
                builder.UseSetting("https_port", portaHttps);

            foreach (var (chave, valor) in infra.Configuracao("t" + Guid.NewGuid().ToString("N")[..11]))
                builder.UseSetting(chave, valor);

            builder.ConfigureTestServices(services =>
                services.AddControllers()
                    .AddApplicationPart(typeof(EncaminhamentoDeTesteController).Assembly)
                    .AddNewtonsoftJson());
        }
    }
}
