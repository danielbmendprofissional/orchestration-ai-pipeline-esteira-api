using Esteira.Integration.Tests.Infraestrutura;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Http
{
    // WebApplicationFactory sobre a API de verdade (Program) apontando pros containers da fixture.
    // Nenhuma rota de teste exige sessão, então o Keycloak fica com authority fake — nenhum token é validado.
    public sealed class ApiDeTeste(InfraestruturaFixture infra) : WebApplicationFactory<Program>
    {
        // Sufixo próprio por teste: filas e exchanges do MassTransit não se misturam.
        public static string AmbienteMensageria { get; } = $"t{Guid.NewGuid():N}"[..12];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // Chaves do contrato da infra (docs/agents/infra-local.md), com Keycloak fake.
            builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
            builder.UseSetting("Keycloak:ClientId", "esteira-api");
            builder.UseSetting("Autenticacao:RequerHttps", "false");

            foreach (var (chave, valor) in infra.Configuracao(AmbienteMensageria))
                builder.UseSetting(chave, valor);

            builder.ConfigureTestServices(services =>
            {
                // O controller e os requests de teste moram neste assembly, não no da Api.
                services.AddControllers()
                    .AddApplicationPart(typeof(ResultadoDeTesteController).Assembly)
                    .AddNewtonsoftJson();

                services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PedidoComValor).Assembly));

                // Validators de teste moram neste assembly; sem isso o pipeline de validação não os enxerga.
                services.AddValidatorsFromAssembly(typeof(PedidoValidado).Assembly);
            });
        }
    }
}
