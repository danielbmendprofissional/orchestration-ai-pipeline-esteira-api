using Esteira.Application.Interfaces.Sessao;
using Esteira.Infra.Cache;
using Esteira.IoC.Configuracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Esteira.IoC
{
    public static class Sessao
    {
        // Sessão BFF no Redis do projeto (ADR-0004). Chaves do contrato da infra, sem valor padrão:
        // sem elas a API não sobe e diz qual falta. Toda chave usa `Redis:KeyPrefix`.
        public static IServiceCollection AddSessaoRedis(this IServiceCollection services, IConfiguration configuration)
        {
            var conexao = Configuracao.Obrigatoria(configuration, "ConnectionStrings:Redis");
            var prefixo = Configuracao.Obrigatoria(configuration, "Redis:KeyPrefix");

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(conexao));
            services.AddSingleton<ISessaoStore>(sp => new RedisSessaoStore(sp.GetRequiredService<IConnectionMultiplexer>(), prefixo));
            services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");

            return services;
        }

        private sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
        {
            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                try
                {
                    await redis.GetDatabase().PingAsync();
                    return HealthCheckResult.Healthy();
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy("Redis indisponível", ex);
                }
            }
        }
    }
}
