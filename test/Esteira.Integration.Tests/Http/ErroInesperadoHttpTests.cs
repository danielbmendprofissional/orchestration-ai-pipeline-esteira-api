using System.ComponentModel;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Esteira.Integration.Tests.Http
{
    // Critério 9 da fatia B2: exception não tratada vira Problem Details 500, sem detalhe técnico,
    // e o log guarda a exception com o mesmo traceId da resposta. Rota inexistente segue 404 sem corpo.
    public class ErroInesperadoHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly ApiDeTeste _api;
        private readonly HttpClient _cliente;

        public ErroInesperadoHttpTests(ApiDeTeste api)
        {
            _api = api;
            _cliente = api.CreateClient();
        }

        [Fact, DisplayName("Deve responder 500 application/problem+json sem a mensagem da exception nem stacktrace")]
        public async Task ExceptionNaoTratada_DeveResponderProblemDetailsSemDetalheTecnico()
        {
            var resposta = await _cliente.GetAsync("/api/teste/excecao", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

            var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            texto.Should().NotContain(PedidoQueLancaHandler.Mensagem)
                .And.NotContain(nameof(InvalidOperationException))
                .And.NotContain("   at ");

            using var documento = JsonDocument.Parse(texto);
            var raiz = documento.RootElement;
            raiz.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
            raiz.GetProperty("title").GetString().Should().Be("Erro inesperado");
            raiz.GetProperty("status").GetInt32().Should().Be(500);
            raiz.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
            raiz.TryGetProperty("detail", out _).Should().BeFalse();
        }

        [Fact, DisplayName("Deve registrar no log a exception com o mesmo traceId da resposta")]
        public async Task ExceptionNaoTratada_DeveLogarComOMesmoTraceId()
        {
            var resposta = await _cliente.GetAsync("/api/teste/excecao", TestContext.Current.CancellationToken);

            var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var documento = JsonDocument.Parse(texto);
            var traceId = documento.RootElement.GetProperty("traceId").GetString();

            var log = _api.Services.GetRequiredService<LogDeTeste>();
            log.Eventos.Should().Contain(evento =>
                evento.Level == LogEventLevel.Error
                && evento.Exception != null
                && evento.Exception.Message == PedidoQueLancaHandler.Mensagem
                && evento.Properties.ContainsKey("TraceId")
                && ((ScalarValue)evento.Properties["TraceId"]).Value as string == traceId);
        }

        [Fact, DisplayName("Deve responder 404 sem corpo quando a rota não existe")]
        public async Task RotaInexistente_DeveResponder404SemCorpo()
        {
            var resposta = await _cliente.GetAsync("/api/rota-que-nao-existe", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }
    }
}
