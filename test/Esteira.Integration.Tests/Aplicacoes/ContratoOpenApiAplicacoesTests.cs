using System.ComponentModel;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Integration.Tests.Http;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // A1.3: o front gera os tipos do OpenAPI; as rotas do cadastro de Aplicação precisam descrever corpo e erros.
    [Trait("Fatia", "A1")]
    public class ContratoOpenApiAplicacoesTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
    {
        private async Task<JsonNode> LerAsync()
            => JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken))!;

        [Theory, DisplayName("Deve descrever no OpenAPI as rotas de Aplicação com os status de sucesso e de erro")]
        [InlineData("/api/v1/aplicacoes", "get", "200", "400")]
        [InlineData("/api/v1/aplicacoes", "post", "201", "409")]
        [InlineData("/api/v1/aplicacoes/{id}", "get", "200", "404")]
        [InlineData("/api/v1/aplicacoes/{id}", "put", "200", "403")]
        [InlineData("/api/v1/aplicacoes/{id}/politicas/{ambiente}", "put", "200", "422")]
        public async Task Rotas(string rota, string metodo, string sucesso, string erro)
        {
            var doc = await LerAsync();

            var respostas = doc["paths"]![rota]![metodo]!["responses"]!.AsObject();

            respostas.ContainsKey(sucesso).Should().BeTrue();
            respostas.ContainsKey(erro).Should().BeTrue();
            respostas[erro]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
        }
    }
}
