using Esteira.Application.Common.Events;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Events;
using MediatR;

namespace Esteira.Application.FluxosDeploy.EventHandlers
{
    public sealed class FluxoDeployConcluidoEventHandler
        : INotificationHandler<DomainEventNotification<FluxoDeployConcluidoEvent>>
    {
        private readonly IDeployRepository _deployRepository;

        public FluxoDeployConcluidoEventHandler(IDeployRepository deployRepository)
        {
            _deployRepository = deployRepository;
        }

        public async Task Handle(
            DomainEventNotification<FluxoDeployConcluidoEvent> notification,
            CancellationToken cancellationToken)
        {
            var deploys = await _deployRepository.ObterPorFluxoEStatusAsync(
                notification.DomainEvent.IdFluxoDeploy,
                StatusEnum.EXECUTANDO,
                cancellationToken);

            foreach (var deploy in deploys)
                deploy.Concluir();
        }
    }
}
