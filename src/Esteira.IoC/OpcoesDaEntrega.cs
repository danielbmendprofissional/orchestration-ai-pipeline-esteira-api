using Esteira.Application.Entregas;
using Microsoft.Extensions.Configuration;

namespace Esteira.IoC
{
    public sealed class OpcoesDaEntrega(IConfiguration configuracao) : IOpcoesDaEntrega
    {
        public bool E1bLigada => bool.TryParse(configuracao["Features:E1b"], out var ligado) && ligado;
    }
}

namespace Esteira.IoC
{
    public sealed class UrlDoGitLab(IConfiguration configuracao) : Esteira.Application.Entregas.Queries.Obter.IUrlDoGitLab
    {
        public string? Base => configuracao["GitLab:Url"];
    }
}
