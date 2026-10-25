using Esteira.Application.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Events;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Esteira.Integration.Tests.Infraestrutura.InfraestruturaFixture))]

namespace Esteira.Integration.Tests.Infraestrutura
{
    // PostgreSQL, RabbitMQ e Redis reais, um trio por execução do assembly. O schema sai das
    // migrations de verdade (as mesmas que rodam no deploy), não de EnsureCreated.
    // O Redis tem o usuário `esteira` com a ACL da infra (ai-infra, users.acl): chaves e canais `esteira:*`, `+@all -@dangerous -scan`.
    public sealed class InfraestruturaFixture : IAsyncLifetime
    {
        public const string UsuarioRedis = "esteira";
        public const string SenhaRedis = "senha-de-teste";
        public const string PrefixoRedis = "esteira:";

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

        public string ConnectionString => _postgres.GetConnectionString();

        // Conexão do projeto (usuário restrito pela ACL), a que a API usa.
        public string RedisConnectionString => $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},user={UsuarioRedis},password={SenhaRedis},abortConnect=false";

        // Conexão administrativa, só para os testes inspecionarem as chaves.
        public string RedisAdminConnectionString => $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)},allowAdmin=true,abortConnect=false";

        public IDictionary<string, string?> Configuracao(string ambienteMensageria) => new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = ConnectionString,
            ["ConnectionStrings:Redis"] = RedisConnectionString,
            ["Redis:KeyPrefix"] = PrefixoRedis,
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
            await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync(), _redis.StartAsync());

            await using (var admin = await ConnectionMultiplexer.ConnectAsync(RedisAdminConnectionString))
            {
                await admin.GetDatabase().ExecuteAsync(
                    "ACL", "SETUSER", UsuarioRedis, "on", $">{SenhaRedis}", $"~{PrefixoRedis}*", $"&{PrefixoRedis}*",
                    "+@all", "-@dangerous", "-scan");
            }

            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(ConnectionString).Options;
            await using var contexto = new EsteiraContext(opcoes, new SemDespacho());
            await contexto.Database.MigrateAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _rabbitMq.DisposeAsync();
            await _redis.DisposeAsync();
        }

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
