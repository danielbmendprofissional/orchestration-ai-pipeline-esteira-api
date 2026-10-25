using System.ComponentModel;
using System.Net;
using AwesomeAssertions;
using Esteira.Data.Common.Persistence;
using Esteira.Integration.Tests.Infraestrutura;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Http
{
    // Critério 7 da fatia B2 ponta a ponta: command que devolve Result de falha não deixa rastro no outbox.
    public class FalhaSemRastroHttpTests(InfraestruturaFixture infra)
    {
        private readonly HttpClient _cliente = new ApiDeTeste(infra).CreateClient();

        [Fact, DisplayName("Não deve gravar no outbox a mensagem publicada por command que devolve Result de falha")]
        public async Task CommandComFalha_NaoDeveGravarMensagemNoOutbox()
        {
            var marcador = Guid.NewGuid();

            var resposta = await _cliente.PostAsync($"/api/teste/publica-e-falha/{marcador}", null, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(infra.ConnectionString).Options;
            await using var contexto = new EsteiraContext(opcoes, null!);
            (await contexto.Set<OutboxMessage>().CountAsync(m => m.Body.Contains(marcador.ToString()), TestContext.Current.CancellationToken))
                .Should().Be(0);
        }
    }
}
