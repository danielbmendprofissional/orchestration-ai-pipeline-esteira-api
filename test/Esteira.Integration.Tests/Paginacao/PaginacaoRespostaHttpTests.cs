using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Paginacao
{
    // ADR-0002, fatia B1.3: o corpo é o array puro e o resto da página vai nos headers X-*.
    [Collection("PaginacaoHttp")]
    public class PaginacaoRespostaHttpTests : IClassFixture<ApiDeTeste>, IAsyncLifetime
    {
        private readonly HttpClient _cliente;
        private readonly InfraestruturaFixture _infra;

        public PaginacaoRespostaHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _cliente = api.CreateClient();
            _infra = infra;
        }

        public async ValueTask InitializeAsync() => await ContextoDePaginacao.SemearAsync(_infra);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<HttpResponseMessage> Pedir(string url, string? pagina = null, string? tamanho = null, string? origem = null)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            if (pagina is not null) pedido.Headers.Add("X-Page", pagina);
            if (tamanho is not null) pedido.Headers.Add("X-Page-Size", tamanho);
            if (origem is not null) pedido.Headers.Add("Origin", origem);
            return await _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        }

        private static string Header(HttpResponseMessage resposta, string nome)
            => resposta.Headers.GetValues(nome).Single();

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
            => await resposta.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        [Fact, DisplayName("Deve responder 200 com o array puro da página e os quatro headers X-*")]
        public async Task ListaValida_DeveResponderArrayPuroEHeaders()
        {
            var resposta = await Pedir("/api/teste/itens", pagina: "2", tamanho: "5");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            corpo.ValueKind.Should().Be(JsonValueKind.Array);
            corpo.GetArrayLength().Should().Be(5);
            corpo[0].GetProperty("id").GetInt64().Should().Be(6);
            Header(resposta, "X-Total-Count").Should().Be("25");
            Header(resposta, "X-Total-Pages").Should().Be("5");
            Header(resposta, "X-Page").Should().Be("2");
            Header(resposta, "X-Page-Size").Should().Be("5");
        }

        [Fact, DisplayName("Deve usar página 1 e tamanho 10 e informá-los nos headers quando o pedido não traz paginação")]
        public async Task SemHeaders_DeveInformarPadroes()
        {
            var resposta = await Pedir("/api/teste/itens");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(resposta)).GetArrayLength().Should().Be(10);
            Header(resposta, "X-Page").Should().Be("1");
            Header(resposta, "X-Page-Size").Should().Be("10");
            Header(resposta, "X-Total-Count").Should().Be("25");
            Header(resposta, "X-Total-Pages").Should().Be("3");
        }

        [Fact, DisplayName("Deve responder 200 com array vazio e total real quando a página passa da última")]
        public async Task PaginaAlemDaUltima_DeveResponderArrayVazioComTotalReal()
        {
            var resposta = await Pedir("/api/teste/itens", pagina: "99");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            corpo.ValueKind.Should().Be(JsonValueKind.Array);
            corpo.GetArrayLength().Should().Be(0);
            Header(resposta, "X-Total-Count").Should().Be("25");
            Header(resposta, "X-Page").Should().Be("99");
        }

        [Fact, DisplayName("Deve ordenar o corpo pelo sort e order pedidos")]
        public async Task SortEOrder_DevemOrdenarOCorpo()
        {
            var resposta = await Pedir("/api/teste/itens?sort=nome&order=desc", tamanho: "3");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var nomes = (await CorpoAsync(resposta)).EnumerateArray().Select(i => i.GetProperty("nome").GetString()).ToArray();
            nomes.Should().Equal("item-25", "item-24", "item-23");
        }

        [Fact, DisplayName("Deve expor os quatro headers X-* no CORS em Development")]
        public async Task OrigemDoFront_DeveExporOsHeadersNoCors()
        {
            var resposta = await Pedir("/api/teste/itens", origem: "http://localhost:4200");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var expostos = string.Join(",", resposta.Headers.GetValues("Access-Control-Expose-Headers"));
            expostos.Should().Contain("X-Total-Count").And.Contain("X-Total-Pages").And.Contain("X-Page,").And.Contain("X-Page-Size");
        }
    }
}
