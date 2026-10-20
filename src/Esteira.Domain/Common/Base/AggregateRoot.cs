using Esteira.Domain.Common.Events;
namespace Esteira.Domain.Common.Base
{
    public abstract class AggregateRoot : EntityBase, IAggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

        protected void AdicionarEvento(IDomainEvent domainEvent)
            => _domainEvents.Add(domainEvent);

        public void LimparEventos()
            => _domainEvents.Clear();
    }
}
