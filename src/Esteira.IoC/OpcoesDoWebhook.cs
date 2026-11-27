using Esteira.Application.Eventos;
using Microsoft.Extensions.Configuration;

namespace Esteira.IoC
{
    // `GitLab:Webhook:Secret` e `Webhook:UrlPublica` (contrato de configuração; nunca em appsettings versionado).
    public sealed class OpcoesDoWebhook(IConfiguration configuracao) : IOpcoesDoWebhook
    {
        public string? Segredo => configuracao["GitLab:Webhook:Secret"];

        public string? UrlPublica => configuracao["Webhook:UrlPublica"];
    }
}
