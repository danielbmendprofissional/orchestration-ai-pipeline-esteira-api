using Esteira.Application.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Events;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(Esteira.Integration.Tests.Infraestrutura.InfraestruturaFixture))]

namespace Esteira.Integration.Tests.Infraestrutura
{
    // PostgreSQL e RabbitMQ reais, um par por execução do assembly. O schema sai das
    // migrations de verdade (as mesmas que rodam no deploy), não de EnsureCreated.
    public sealed class InfraestruturaFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

        public string ConnectionString => _postgres.GetConnectionString();

        public IDictionary<string, string?> Configuracao(string ambienteMensageria) => new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = ConnectionString,
            ["RabbitMQ:Host"] = _rabbitMq.Hostname,
            ["RabbitMQ:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMQ:VirtualHost"] = "/",
            ["RabbitMQ:UserName"] = RabbitMqBuilder.DefaultUsername,
            ["RabbitMQ:Password"] = RabbitMqBuilder.DefaultPassword,
            // Sufixo próprio por teste: filas e exchanges não se misturam entre testes.
            ["RabbitMQ:Ambiente"] = ambienteMensageria
        };

        public async ValueTask InitializeAsync()
        {
            await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UseNpgsql(ConnectionString).Options;
            await using var contexto = new EsteiraContext(opcoes, new SemDespacho());
            await contexto.Database.MigrateAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _rabbitMq.DisposeAsync();
        }

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
