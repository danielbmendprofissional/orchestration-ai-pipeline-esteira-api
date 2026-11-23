using Esteira.Application.Interfaces.Fatias;
using Microsoft.Extensions.Configuration;

namespace Esteira.IoC
{
    // `Features:P2` (só `true` liga; ausente = desligado) e `Review:UrlModelo` (não secreto), lidos da configuração a cada uso.
    public sealed class OpcoesDeRevisao(IConfiguration configuracao) : IOpcoesDeRevisao
    {
        public bool Ligado => bool.TryParse(configuracao["Features:P2"], out var ligado) && ligado;

        public string? UrlModelo => configuracao["Review:UrlModelo"];
    }
}
