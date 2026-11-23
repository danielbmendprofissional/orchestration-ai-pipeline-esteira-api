using System.ComponentModel;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Integration.Tests.Http;

namespace Esteira.Integration.Tests.Fatias
{
    // P2.5: o front gera os tipos do OpenAPI; as três rotas e os campos novos do Painel e da gaveta precisam estar descritos.
    [Trait("Fatia", "P2")]
    public class ContratoOpenApiRevisaoTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
    {
        private async Task<JsonNode> LerAsync()
            => JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken))!;

        [Theory, DisplayName("Descreve as rotas de decisão com 202 e os erros de negócio como RespostaDeErro")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/review", "409")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/review", "403")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/aprovar", "409")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/aprovar", "404")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/reprovar", "400")]
        [InlineData("/api/v1/aplicacoes/{id}/fatias/{codigo}/reprovar", "409")]
        public async Task Rotas(string rota, string erro)
        {
            var doc = await LerAsync();

            var respostas = doc["paths"]![rota]!["post"]!["responses"]!.AsObject();

            respostas.ContainsKey("202").Should().BeTrue();
            respostas["202"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/AcoesPedidasDto");
            respostas.ContainsKey(erro).Should().BeTrue();
            respostas[erro]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
        }

        [Fact, DisplayName("Descreve o corpo do Reprovar com o comentário")]
        public async Task CorpoDoReprovar()
        {
            var doc = await LerAsync();

            var schemas = doc["components"]!["schemas"]!.AsObject();

            schemas["ReprovarFatiaRequest"]!["properties"]!.AsObject().ContainsKey("comentario").Should().BeTrue();
            schemas["AcoesPedidasDto"]!["properties"]!["acoes"].Should().NotBeNull();
        }

        [Fact, DisplayName("Descreve acoes, review e decisao na linha do Painel e na gaveta, e a lista de Ações da gaveta")]
        public async Task CamposNovos()
        {
            var doc = await LerAsync();

            var schemas = doc["components"]!["schemas"]!.AsObject();

            foreach (var dto in new[] { "FatiaDoPainelDto", "GavetaDto" })
                foreach (var campo in new[] { "acoes", "review", "decisao" })
                    schemas[dto]!["properties"]!.AsObject().ContainsKey(campo).Should().BeTrue($"{dto}.{campo}");
            schemas["GavetaDto"]!["properties"]!.AsObject().ContainsKey("acoesPedidas").Should().BeTrue();
            schemas["AcoesDaFatiaDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("podeAbrirReview", "podeAprovar", "podeReprovar");
            schemas["PermissaoDaAcaoDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("pode", "motivo");
            schemas["ReviewDaFatiaDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("estado", "link");
            schemas["AcaoDaFatiaDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().BeEquivalentTo("id", "tipo", "estado", "repositorio", "mrIid", "pedidaPor", "pedidaEm", "atualizadaEm", "codigoErro", "comentario");
        }
    }
}
