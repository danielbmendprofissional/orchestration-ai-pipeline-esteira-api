using Esteira.AcL.GitLab;
using Esteira.Application.Interfaces.Integracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.IoC
{
    public static class GitLab
    {
        // Porta IGitLab (só leitura) com HttpClient tipado. `GitLab:Url` e `GitLab:Bots:<grupo>:Token` ficam em user-secrets;
        // sem eles a API sobe normal e as chamadas respondem gitlab.bot-nao-configurado.
        public static IServiceCollection AddGitLab(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GitLabOptions>(configuration.GetSection(GitLabOptions.Secao));

            services.AddHttpClient<IGitLab, GitLabHttp>(cliente =>
            {
                cliente.Timeout = TimeSpan.FromSeconds(10);
                if (Uri.TryCreate(configuration[$"{GitLabOptions.Secao}:Url"]?.TrimEnd('/') + "/", UriKind.Absolute, out var url))
                    cliente.BaseAddress = url;
            });

            // T1.4: hooks do projeto (registro do webhook), mesmo token do bot do grupo.
            services.AddHttpClient<IGitLabHooks, GitLabHooksHttp>(cliente =>
            {
                cliente.Timeout = TimeSpan.FromSeconds(10);
                if (Uri.TryCreate(configuration[$"{GitLabOptions.Secao}:Url"]?.TrimEnd('/') + "/", UriKind.Absolute, out var url))
                    cliente.BaseAddress = url;
            });

            return services;
        }
    }
}
