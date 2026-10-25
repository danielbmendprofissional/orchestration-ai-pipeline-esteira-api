using AwesomeAssertions;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Esteira.Integration.Tests.Sessao
{
    public sealed class ConfiguracaoDoRedisTests(InfraestruturaFixture infra)
    {
        private sealed class ApiSemChave(InfraestruturaFixture infra, string ausente) : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
                builder.UseSetting("Keycloak:ClientId", "esteira-api");
                builder.UseSetting("Keycloak:ClientSecret", "segredo-de-teste");
                builder.UseSetting("Autenticacao:UrlPublica", "http://localhost:4200");

                foreach (var (chave, valor) in infra.Configuracao("t" + Guid.NewGuid().ToString("N")[..11]))
                    builder.UseSetting(chave, chave == ausente ? null : valor);
            }
        }

        [Theory]
        [InlineData("ConnectionStrings:Redis")]
        [InlineData("Redis:KeyPrefix")]
        public void Sem_a_chave_do_Redis_a_API_nao_sobe_e_diz_qual_falta(string chave)
        {
            using var api = new ApiSemChave(infra, chave);

            var erro = Record.Exception(() => api.CreateClient());

            erro.Should().NotBeNull();
            erro!.ToString().Should().Contain($"'{chave}'");
        }
    }
}
