using Esteira.Domain.Common.Events;
namespace Esteira.Application.Common.Events
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default);
    }
}
