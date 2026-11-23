using Esteira.Application.Fatias;
using Esteira.Infra.Cache;
using Esteira.IoC.Configuracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Esteira.IoC
{
    public static class Painel
    {
        // Serviço do Painel de Fatias (P1.5). Precisa do IConnectionMultiplexer (AddSessaoRedis) e do IGitLab (AddGitLab).
        // `Painel:ValidadeSegundos` (padrão 60) é chave de aplicação, não de infra.
        public static IServiceCollection AddPainelDeFatias(this IServiceCollection services, IConfiguration configuration)
        {
            var prefixo = Configuracao.Obrigatoria(configuration, "Redis:KeyPrefix");
            var opcoes = new PainelOptions();
            configuration.GetSection(PainelOptions.Secao).Bind(opcoes);

            services.AddSingleton(opcoes);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IPainelCache>(sp => new RedisPainelCache(sp.GetRequiredService<IConnectionMultiplexer>(), prefixo));
            services.AddScoped<PainelDeFatias>();
            services.AddScoped<IPainelDeFatias>(sp => sp.GetRequiredService<PainelDeFatias>());
            services.AddScoped<IDescarteDoPainel>(sp => sp.GetRequiredService<PainelDeFatias>());
            services.AddSingleton(sp => new LinksDoGitLab(sp.GetRequiredService<IConfiguration>()["GitLab:Url"]));

            return services;
        }
    }
}
