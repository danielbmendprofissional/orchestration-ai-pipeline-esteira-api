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
        [InlineData("/api/v1/aplicacoes/{id}/gitlab/projetos", "get", "200", "403")]
        [InlineData("/api/v1/aplicacoes/{id}/projetos", "post", "201", "409")]
        [InlineData("/api/v1/projetos/{id}", "put", "200", "403")]
        [InlineData("/api/v1/projetos/{id}/dependencias", "put", "200", "400")]
        [InlineData("/api/v1/projetos/{id}/ambientes/{ambiente}", "put", "200", "400")]
        [InlineData("/api/v1/projetos/{id}/ambientes/{ambiente}/destinos", "post", "201", "409")]
        [InlineData("/api/v1/projetos/{id}/ambientes/{ambiente}/destinos/{destinoId}", "put", "200", "404")]
        [InlineData("/api/v1/projetos/{id}/ambientes/{ambiente}/destinos/{destinoId}", "delete", "204", "404")]
        [InlineData("/api/v1/pessoas", "get", "200", "403")]
        [InlineData("/api/v1/aplicacoes/{id}/papeis", "get", "200", "404")]
        [InlineData("/api/v1/aplicacoes/{id}/papeis", "post", "201", "409")]
        [InlineData("/api/v1/aplicacoes/{id}/papeis", "post", "201", "422")]
        [InlineData("/api/v1/aplicacoes/{id}/papeis/{papelId}", "delete", "204", "404")]
        public async Task Rotas(string rota, string metodo, string sucesso, string erro)
        {
            var doc = await LerAsync();

            var respostas = doc["paths"]![rota]![metodo]!["responses"]!.AsObject();

            respostas.ContainsKey(sucesso).Should().BeTrue();
            respostas.ContainsKey(erro).Should().BeTrue();
            respostas[erro]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
        }

        [Fact, DisplayName("Deve descrever no OpenAPI o papel (com nome e e-mail da pessoa) e o item da busca de pessoas só com sub, nome e e-mail")]
        public async Task SchemasDePapelEPessoa()
        {
            var doc = await LerAsync();

            var schemas = doc["components"]!["schemas"]!.AsObject();

            var papel = schemas["PapelResumoDto"]!["properties"]!.AsObject();
            foreach (var campo in new[] { "id", "usuario", "nome", "email", "papel" })
                papel.ContainsKey(campo).Should().BeTrue();
            schemas["PessoaListaDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("sub", "nome", "email");
        }

        [Fact, DisplayName("Deve descrever no OpenAPI o Projeto completo (ambientes com destinos e dependeDe) e o item de projeto do GitLab")]
        public async Task SchemasDeProjeto()
        {
            var doc = await LerAsync();

            var schemas = doc["components"]!["schemas"]!.AsObject();

            var projeto = schemas["ProjetoResumoDto"]!["properties"]!.AsObject();
            projeto.ContainsKey("ambientes").Should().BeTrue();
            projeto.ContainsKey("dependeDe").Should().BeTrue();
            projeto.ContainsKey("stack").Should().BeTrue();
            schemas["AmbienteProjetoDto"]!["properties"]!.AsObject().ContainsKey("destinos").Should().BeTrue();
            schemas["DestinoDto"]!["properties"]!.AsObject().ContainsKey("configuracao").Should().BeTrue();
            schemas["ProjetoGitLabItemDto"]!["properties"]!.AsObject().ContainsKey("stackSugerida").Should().BeTrue();
        }
    }
}
