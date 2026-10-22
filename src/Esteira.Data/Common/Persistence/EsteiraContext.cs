using Esteira.Application.Common.Events;
using Esteira.Domain.Common.Base;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence
{
    public sealed class EsteiraContext : DbContext
    {
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public EsteiraContext(
            DbContextOptions<EsteiraContext> options,
            IDomainEventDispatcher domainEventDispatcher)
            : base(options)
        {
            _domainEventDispatcher = domainEventDispatcher;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Outbox e inbox do MassTransit (ADR-0010): mensagem publicada num command
            // grava na mesma transação do SaveChanges.
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await DespacharEventosAsync(cancellationToken);

            return await base.SaveChangesAsync(cancellationToken);
        }

        private async Task DespacharEventosAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var raizes = ChangeTracker
                    .Entries<AggregateRoot>()
                    .Select(e => e.Entity)
                    .Where(e => e.DomainEvents.Count > 0)
                    .ToList();

                if (raizes.Count == 0)
                    return;

                var eventos = raizes
                    .SelectMany(r => r.DomainEvents)
                    .ToList();

                raizes.ForEach(r => r.LimparEventos());

                await _domainEventDispatcher.DispatchAsync(eventos, cancellationToken);
            }
        }
    }
}
