using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Http
{
    // ADR-0003, fatia B2: o filtro de resultado traduz Result → HTTP num ponto só.
    public class ResultadoHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly HttpClient _cliente;

        public ResultadoHttpTests(ApiDeTeste api)
        {
            _cliente = api.CreateClient();
        }

        private sealed record ErroNaResposta(string Code, string Message, string Type);

        private sealed record CorpoComErros(List<ErroNaResposta> Errors);

        [Fact, DisplayName("Deve responder 200 com o valor quando o Result de sucesso tem valor")]
        public async Task SucessoComValor_DeveResponder200ComOValor()
        {
            var resposta = await _cliente.GetAsync("/api/teste/valor", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("valor de teste");
        }

        [Fact, DisplayName("Deve responder 204 sem corpo quando o Result de sucesso não tem valor")]
        public async Task SucessoSemValor_DeveResponder204SemCorpo()
        {
            var resposta = await _cliente.GetAsync("/api/teste/sem-valor", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }

        [Theory, DisplayName("Deve responder o status do tipo do erro com o corpo errors[]")]
        [InlineData(TipoErro.Validation, HttpStatusCode.BadRequest)]
        [InlineData(TipoErro.Unauthorized, HttpStatusCode.Unauthorized)]
        [InlineData(TipoErro.Forbidden, HttpStatusCode.Forbidden)]
        [InlineData(TipoErro.NotFound, HttpStatusCode.NotFound)]
        [InlineData(TipoErro.Conflict, HttpStatusCode.Conflict)]
        [InlineData(TipoErro.Failure, HttpStatusCode.UnprocessableEntity)]
        public async Task Falha_DeveResponderStatusDoTipoDoErro(TipoErro tipo, HttpStatusCode esperado)
        {
            var resposta = await _cliente.GetAsync($"/api/teste/falha/{tipo}", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(esperado);

            var corpo = await resposta.Content.ReadFromJsonAsync<CorpoComErros>(TestContext.Current.CancellationToken);
            corpo.Should().NotBeNull();
            corpo!.Errors.Should().HaveCount(1);

            var erro = ErrosDeTeste.ErroDo(tipo);
            corpo.Errors[0].Code.Should().Be(erro.Code);
            corpo.Errors[0].Message.Should().Be(erro.Message);
            corpo.Errors[0].Type.Should().Be(tipo.ToString());
        }

        [Fact, DisplayName("Deve responder 400 do primeiro erro e trazer os dois erros na ordem")]
        public async Task FalhaComDoisErros_DeveUsarOStatusDoPrimeiroEManterAOrdem()
        {
            var resposta = await _cliente.GetAsync("/api/teste/duas-falhas", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var corpo = await resposta.Content.ReadFromJsonAsync<CorpoComErros>(TestContext.Current.CancellationToken);
            corpo.Should().NotBeNull();
            corpo!.Errors.Should().HaveCount(2);

            corpo.Errors[0].Code.Should().Be("url.invalida");
            corpo.Errors[0].Type.Should().Be("Validation");
            corpo.Errors[1].Code.Should().Be("projeto.nao.encontrado");
            corpo.Errors[1].Type.Should().Be("NotFound");

            // O corpo é só { errors: [...] }, nunca o envelope Result serializado.
            (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
                .Should().NotContain("isSuccess").And.NotContain("value");
        }
    }
}
