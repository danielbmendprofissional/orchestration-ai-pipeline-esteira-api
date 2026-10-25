using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Http
{
    // Critérios 4, 5 e 6 da fatia B2: validação e model binding saem em { errors: [...] },
    // sem exception e sem o ValidationProblemDetails padrão do [ApiController].
    public class ValidacaoHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly HttpClient _cliente;

        public ValidacaoHttpTests(ApiDeTeste api)
        {
            _cliente = api.CreateClient();
        }

        [Fact, DisplayName("Deve responder 400 com um erro Validation por falha e não executar o handler")]
        public async Task FalhaDeValidacao_DeveResponder400SemExecutarHandler()
        {
            PedidoValidadoHandler.Execucoes = 0;

            var resposta = await _cliente.PostAsJsonAsync(
                "/api/teste/validado", new { url = "" }, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var corpo = await resposta.Content.ReadFromJsonAsync<CorpoComErros>(TestContext.Current.CancellationToken);
            corpo.Should().NotBeNull();
            corpo!.Errors.Should().HaveCount(2);
            corpo.Errors.Select(e => e.Code).Should().Equal("url.obrigatoria", "url.invalida");
            corpo.Errors.Select(e => e.Message).Should().Equal("A url é obrigatória.", "A url informada é inválida.");
            corpo.Errors.Should().OnlyContain(e => e.Type == "Validation");

            PedidoValidadoHandler.Execucoes.Should().Be(0);
        }

        [Fact, DisplayName("Deve usar validacao.campo-invalido quando o validator não declara ErrorCode")]
        public async Task ValidatorSemErrorCode_DeveUsarCodigoPadrao()
        {
            PedidoSemErrorCodeHandler.Execucoes = 0;

            var resposta = await _cliente.PostAsJsonAsync(
                "/api/teste/sem-error-code", new { nome = "" }, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var corpo = await resposta.Content.ReadFromJsonAsync<CorpoComErros>(TestContext.Current.CancellationToken);
            corpo.Should().NotBeNull();
            corpo!.Errors.Should().ContainSingle();
            corpo.Errors[0].Code.Should().Be("validacao.campo-invalido");
            corpo.Errors[0].Message.Should().Be("O nome é obrigatório.");
            corpo.Errors[0].Type.Should().Be("Validation");

            PedidoSemErrorCodeHandler.Execucoes.Should().Be(0);
        }

        [Theory, DisplayName("Deve responder 400 requisicao.invalida em errors[] quando o corpo é ilegível ou tem tipo errado")]
        [InlineData("{\"quantidade\": \"muitas\"}")]
        [InlineData("{\"quantidade\":")]
        public async Task CorpoInvalido_DeveResponder400RequisicaoInvalida(string json)
        {
            PedidoComCorpoHandler.Execucoes = 0;

            var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
            var resposta = await _cliente.PostAsync(
                "/api/teste/corpo", conteudo, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var documento = JsonDocument.Parse(texto);
            var raiz = documento.RootElement;

            // Nunca o ValidationProblemDetails padrão: sem title/type de topo e errors é array.
            raiz.TryGetProperty("title", out _).Should().BeFalse();
            raiz.TryGetProperty("type", out _).Should().BeFalse();
            raiz.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Array);

            var erros = raiz.GetProperty("errors");
            erros.GetArrayLength().Should().Be(1);
            erros[0].GetProperty("code").GetString().Should().Be("requisicao.invalida");
            erros[0].GetProperty("message").GetString().Should().Be("A requisição é inválida.");
            erros[0].GetProperty("type").GetString().Should().Be("Validation");

            PedidoComCorpoHandler.Execucoes.Should().Be(0);
        }

        private sealed record ErroNaResposta(string Code, string Message, string Type);

        private sealed record CorpoComErros(List<ErroNaResposta> Errors);
    }
}
