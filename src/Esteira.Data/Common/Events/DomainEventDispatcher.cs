using Esteira.Application.Common.Events;
using Esteira.Domain.Common.Events;
using MediatR;

namespace Esteira.Data.Common.Events
{
    public sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IPublisher _publisher;

        public DomainEventDispatcher(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
        {
            foreach (var evento in eventos)
            {
                var tipoNotificacao = typeof(DomainEventNotification<>).MakeGenericType(evento.GetType());
                var notificacao = (INotification)Activator.CreateInstance(tipoNotificacao, evento)!;

                await _publisher.Publish(notificacao, cancellationToken);
            }
        }
    }
}
