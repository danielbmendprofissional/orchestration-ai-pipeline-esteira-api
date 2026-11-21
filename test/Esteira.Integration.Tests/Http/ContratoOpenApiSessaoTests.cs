using System.ComponentModel;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Esteira.Integration.Tests.Http
{
    // F0.5, critério 17: o front gera os tipos do OpenAPI e não declara interface própria para payload.
    // Por isso a sessão (ADR-0004) precisa descrever no contrato o corpo do `me`, o 204 do refresh e o corpo dos 401.
    [Trait("Fatia", "F0.5")]
    public class ContratoOpenApiSessaoTests : IClassFixture<ApiDeTeste>
    {
        private readonly HttpClient _cliente;

        public ContratoOpenApiSessaoTests(ApiDeTeste api)
        {
            _cliente = api.CreateClient();
        }

        private async Task<JsonNode> LerOpenApiAsync()
        {
            var json = await _cliente.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
            return JsonNode.Parse(json)!;
        }

        private static JsonNode Operacao(JsonNode doc, string rota, string metodo)
            => doc["paths"]![rota]![metodo]!;

        private static JsonNode Schema(JsonNode doc, string nome)
            => doc["components"]!["schemas"]![nome]!;

        private static string? RefDoCorpo(JsonNode operacao, string status)
            => operacao["responses"]![status]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();

        [Fact, DisplayName("Deve descrever no OpenAPI o corpo 200 do /usuarios/me com usuario, nome, email e perfis")]
        public async Task Me_DeveDescreverCorpoDoUsuarioLogado()
        {
            var doc = await LerOpenApiAsync();

            var referencia = RefDoCorpo(Operacao(doc, "/api/v1/usuarios/me", "get"), "200");

            referencia.Should().Be("#/components/schemas/UsuarioLogadoDto");
            var propriedades = Schema(doc, "UsuarioLogadoDto")["properties"]!.AsObject().Select(p => p.Key).ToArray();
            propriedades.Should().BeEquivalentTo("usuario", "nome", "email", "perfis");
        }

        [Fact, DisplayName("Deve descrever no OpenAPI o 204 sem corpo do refresh, e não um 200 com Result")]
        public async Task Refresh_DeveDescrever204SemCorpo()
        {
            var doc = await LerOpenApiAsync();

            var respostas = Operacao(doc, "/api/v1/auth/refresh", "post")["responses"]!.AsObject();

            respostas.ContainsKey("204").Should().BeTrue();
            respostas.ContainsKey("200").Should().BeFalse();
        }

        [Theory, DisplayName("Deve descrever no OpenAPI o corpo errors[] dos erros de sessão")]
        [InlineData("/api/v1/usuarios/me", "get", "401")]
        [InlineData("/api/v1/auth/refresh", "post", "401")]
        [InlineData("/api/v1/auth/refresh", "post", "422")]
        public async Task Erros_DevemReferenciarARespostaDeErro(string rota, string metodo, string status)
        {
            var doc = await LerOpenApiAsync();

            var referencia = RefDoCorpo(Operacao(doc, rota, metodo), status);

            referencia.Should().Be("#/components/schemas/RespostaDeErro");
            Schema(doc, "RespostaDeErro")["properties"]!.AsObject().ContainsKey("errors").Should().BeTrue();
        }
    }
}
