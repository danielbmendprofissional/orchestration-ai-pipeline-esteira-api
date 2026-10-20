using Esteira.Application.Common.Events;
using Esteira.Domain.Common.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Esteira.Data.Common.Persistence
{
    public sealed class EsteiraContextFactory : IDesignTimeDbContextFactory<EsteiraContext>
    {
        public EsteiraContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ESTEIRA_CONNECTION")
                ?? "Host=localhost;Database=esteira;Username=postgres;Password=postgres";

            var options = new DbContextOptionsBuilder<EsteiraContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new EsteiraContext(options, new SemDespacho());
        }

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
