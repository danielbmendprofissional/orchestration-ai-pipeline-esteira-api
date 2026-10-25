using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Paginacao
{
    // ADR-0002, fatia B1.2: pedido inválido é recusado com 400 antes de qualquer consulta.
    [Collection("PaginacaoHttp")] // contador estático do handler: não roda em paralelo com as outras classes que listam
    public class PaginacaoHttpTests : IClassFixture<ApiDeTeste>, IAsyncLifetime
    {
        private readonly HttpClient _cliente;
        private readonly InfraestruturaFixture _infra;

        public PaginacaoHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _cliente = api.CreateClient();
            _infra = infra;
        }

        public async ValueTask InitializeAsync()
        {
            await ContextoDePaginacao.SemearAsync(_infra);
            ListarItensDeTesteHandler.Execucoes = 0;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed record ErroNaResposta(string Code, string Message, string Type);

        private sealed record CorpoComErros(List<ErroNaResposta> Errors);

        private async Task<HttpResponseMessage> Pedir(string url, string? pagina = null, string? tamanho = null)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            if (pagina is not null) pedido.Headers.Add("X-Page", pagina);
            if (tamanho is not null) pedido.Headers.Add("X-Page-Size", tamanho);
            return await _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        }

        private static async Task<string[]> CodigosAsync(HttpResponseMessage resposta)
        {
            var corpo = await resposta.Content.ReadFromJsonAsync<CorpoComErros>(TestContext.Current.CancellationToken);
            return corpo!.Errors.Select(e => e.Code).ToArray();
        }

        [Theory, DisplayName("Deve responder 400 paginacao.pagina-invalida sem executar a consulta")]
        [InlineData("abc")]
        [InlineData("1.5")]
        [InlineData("0")]
        [InlineData("-1")]
        public async Task PaginaInvalida_DeveResponder400SemConsultar(string pagina)
        {
            var resposta = await Pedir("/api/teste/itens", pagina: pagina);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigosAsync(resposta)).Should().Equal("paginacao.pagina-invalida");
            ListarItensDeTesteHandler.Execucoes.Should().Be(0);
        }

        [Theory, DisplayName("Deve responder 400 paginacao.tamanho-invalido sem executar a consulta")]
        [InlineData("abc")]
        [InlineData("1.5")]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("101")]
        public async Task TamanhoInvalido_DeveResponder400SemConsultar(string tamanho)
        {
            var resposta = await Pedir("/api/teste/itens", tamanho: tamanho);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigosAsync(resposta)).Should().Equal("paginacao.tamanho-invalido");
            ListarItensDeTesteHandler.Execucoes.Should().Be(0);
        }

        [Fact, DisplayName("Deve responder 400 paginacao.sentido-invalido quando order não é asc nem desc")]
        public async Task SentidoInvalido_DeveResponder400()
        {
            var resposta = await Pedir("/api/teste/itens?order=cima");

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigosAsync(resposta)).Should().Equal("paginacao.sentido-invalido");
            ListarItensDeTesteHandler.Execucoes.Should().Be(0);
        }

        [Fact, DisplayName("Deve responder 400 paginacao.ordenacao-invalida quando sort não é campo do DTO")]
        public async Task SortInexistente_DeveResponder400()
        {
            var resposta = await Pedir("/api/teste/itens?sort=naoExiste");

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigosAsync(resposta)).Should().Equal("paginacao.ordenacao-invalida");
        }

        [Fact, DisplayName("Deve devolver todos os erros de validação de uma vez em errors[]")]
        public async Task VariosInvalidos_DeveDevolverTodosEmErrors()
        {
            var resposta = await Pedir("/api/teste/itens?order=cima", pagina: "0", tamanho: "500");

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigosAsync(resposta)).Should().Equal(
                "paginacao.pagina-invalida", "paginacao.tamanho-invalido", "paginacao.sentido-invalido");
            ListarItensDeTesteHandler.Execucoes.Should().Be(0);
        }
    }
}
