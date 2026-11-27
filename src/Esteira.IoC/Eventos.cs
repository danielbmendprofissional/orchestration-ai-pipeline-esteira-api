using Esteira.Application.Eventos;
using Esteira.Infra.Cache;
using Esteira.IoC.Configuracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Esteira.IoC
{
    public static class Eventos
    {
        // Tempo real (ADR-0011, T1.2): publicador no canal `<Redis:KeyPrefix>eventos`, UMA assinatura por instância e o
        // fan-out em memória para os streams SSE. Precisa do IConnectionMultiplexer (AddSessaoRedis).
        public static IServiceCollection AddEventosDeTempoReal(this IServiceCollection services, IConfiguration configuration)
        {
            var prefixo = Configuracao.Obrigatoria(configuration, "Redis:KeyPrefix");

            services.AddSingleton<DistribuidorDeEventos>();
            services.AddSingleton<IDistribuidorDeEventos>(sp => sp.GetRequiredService<DistribuidorDeEventos>());
            services.AddSingleton<IPublicadorDeEventos>(sp => new RedisPublicadorDeEventos(
                sp.GetRequiredService<IConnectionMultiplexer>(), prefixo, sp.GetRequiredService<ILogger<RedisPublicadorDeEventos>>()));
            services.AddHostedService(sp => new RedisAssinaturaDeEventos(
                sp.GetRequiredService<IConnectionMultiplexer>(), sp.GetRequiredService<IDistribuidorDeEventos>(), prefixo,
                sp.GetRequiredService<ILogger<RedisAssinaturaDeEventos>>()));

            return services;
        }
    }
}
