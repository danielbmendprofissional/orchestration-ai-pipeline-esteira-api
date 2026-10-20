using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Commands.AtualizarAnaliseDast;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Messaging;
using MassTransit;
using MediatR;
using NSubstitute;

namespace Esteira.Application.Tests.Deploys.Consumers
{
    public class AtualizarAnaliseDastConsumerTests
    {
        private readonly IMediator _mediator = Substitute.For<IMediator>();
        private readonly IScannerDast _scannerDast = Substitute.For<IScannerDast>();

        private static ConsumeContext<AtualizarAnaliseDastMessage> Contexto()
        {
            var contexto = Substitute.For<ConsumeContext<AtualizarAnaliseDastMessage>>();
            contexto.Message.Returns(new AtualizarAnaliseDastMessage { IdAnalise = 63, ScanId = "scan-1" });
            return contexto;
        }

        [Fact, DisplayName("Não deve falhar quando o scan ainda não terminou e o scanner DAST não devolver relatório")]
        public async Task ScanNaoFinalizado_NaoDeveFalhar()
        {
            _scannerDast.ObterArtefatosAnalise("scan-1", Arg.Any<Action<string>>())
                .Returns(ci =>
                {
                    ci.Arg<Action<string>>()("running");
                    return Task.FromResult<IEnumerable<ArtefatoCompletoDto>>(null);
                });

            var act = () => new AtualizarAnaliseDastConsumer(_mediator, _scannerDast).Consume(Contexto());

            await act.Should().NotThrowAsync();
            await _mediator.Received(1).Send(
                Arg.Is<AtualizarAnaliseDastCommand>(c => c.IdAnalise == 63 && c.StatusScan == "running" && c.Artefatos.Count == 0),
                Arg.Any<CancellationToken>());
        }
    }
}
