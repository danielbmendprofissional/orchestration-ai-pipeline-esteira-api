using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Common.Events;
using Esteira.Application.FluxosDeploy.EventHandlers;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Events;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.EventHandlers
{
    public class FluxoDeployEventHandlersTests
    {
        private readonly IDeployRepository _deployRepo = Substitute.For<IDeployRepository>();

        [Fact, DisplayName("Deve concluir os deploys em execução do fluxo concluído")]
        public async Task Concluido_DeveConcluirDeploysEmExecucao()
        {
            var deploy = Construtor.Deploy(1, idFluxoDeploy: 7);
            _deployRepo.ObterPorFluxoEStatusAsync(7, StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>()).Returns(new[] { deploy });
            var notificacao = new DomainEventNotification<FluxoDeployConcluidoEvent>(new FluxoDeployConcluidoEvent(7, DateTime.Now));

            await new FluxoDeployConcluidoEventHandler(_deployRepo).Handle(notificacao, CancellationToken.None);

            deploy.Status.Should().Be(StatusEnum.CONCLUIDO);
        }

        [Fact, DisplayName("Deve cancelar todos os deploys e análises do fluxo cancelado")]
        public async Task Cancelado_DeveCancelarDeploysEAnalises()
        {
            var executando = Construtor.Deploy(1, idFluxoDeploy: 7).ComAnalise();
            var concluido = Construtor.Deploy(2, idFluxoDeploy: 7, status: StatusEnum.CONCLUIDO).ComAnalise();
            _deployRepo.ObterPorFluxosAsync(Arg.Is<IEnumerable<int>>(ids => ids.Single() == 7), Arg.Any<CancellationToken>())
                .Returns(new[] { executando, concluido });
            var notificacao = new DomainEventNotification<FluxoDeployCanceladoEvent>(new FluxoDeployCanceladoEvent(7, DateTime.Now));

            await new FluxoDeployCanceladoEventHandler(_deployRepo).Handle(notificacao, CancellationToken.None);

            new[] { executando, concluido }.Should().OnlyContain(d => d.Status == StatusEnum.CANCELADO);
            executando.Analises.Concat(concluido.Analises).Should().OnlyContain(a => a.Status == StatusEnum.CANCELADO);
        }
    }
}
