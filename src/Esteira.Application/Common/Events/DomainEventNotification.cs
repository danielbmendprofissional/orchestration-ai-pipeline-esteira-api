using Esteira.Domain.Common.Events;
using MediatR;

namespace Esteira.Application.Common.Events
{
    public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
        where TEvent : IDomainEvent;
}
