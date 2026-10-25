using AwesomeAssertions;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Esteira.Integration.Tests.Sessao
{
    // Critério 17 da B3 (parte do Keycloak/URL pública): sem a chave a API não sobe e diz qual falta.
    public sealed class ConfiguracaoDaSessaoTests(InfraestruturaFixture infra)
    {
        private sealed class ApiSemChave(InfraestruturaFixture infra, string ausente) : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Staging: não carrega o appsettings.Development.json, então nada preenche a UrlPublica por baixo.
                builder.UseEnvironment("Staging");
                builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
                builder.UseSetting("Keycloak:ClientId", "esteira-api");
                builder.UseSetting("Autenticacao:RequerHttps", "false");

                var chaves = new Dictionary<string, string?>
                {
                    ["Keycloak:ClientSecret"] = "segredo-de-teste",
                    ["Autenticacao:UrlPublica"] = "https://esteira.exemplo"
                };

                foreach (var (chave, valor) in infra.Configuracao("t" + Guid.NewGuid().ToString("N")[..11]).Concat(chaves))
                    builder.UseSetting(chave, chave == ausente ? null : valor);
            }
        }

        [Theory]
        [InlineData("Keycloak:ClientSecret")]
        [InlineData("Autenticacao:UrlPublica")]
        public void Sem_a_chave_a_API_nao_sobe_e_diz_qual_falta(string chave)
        {
            using var api = new ApiSemChave(infra, chave);

            var erro = Record.Exception(() => api.CreateClient());

            erro.Should().NotBeNull();
            erro!.ToString().Should().Contain($"'{chave}'");
        }
    }
}
