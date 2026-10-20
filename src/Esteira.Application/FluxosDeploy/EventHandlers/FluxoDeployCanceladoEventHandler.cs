using Esteira.Application.Common.Events;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Events;
using MediatR;

namespace Esteira.Application.FluxosDeploy.EventHandlers
{
    public sealed class FluxoDeployCanceladoEventHandler
        : INotificationHandler<DomainEventNotification<FluxoDeployCanceladoEvent>>
    {
        private readonly IDeployRepository _deployRepository;

        public FluxoDeployCanceladoEventHandler(IDeployRepository deployRepository)
        {
            _deployRepository = deployRepository;
        }

        public async Task Handle(
            DomainEventNotification<FluxoDeployCanceladoEvent> notification,
            CancellationToken cancellationToken)
        {
            var deploys = await _deployRepository.ObterPorFluxosAsync(
                new[] { notification.DomainEvent.IdFluxoDeploy },
                cancellationToken);

            foreach (var deploy in deploys)
                deploy.Cancelar();
        }
    }
}
