using Esteira.Application.Interfaces.Fatias;
using Microsoft.Extensions.Configuration;

namespace Esteira.IoC
{
    // `Features:P2` (só `true` liga; ausente = desligado) e `Review:*` (não secreto), lidos da configuração a cada uso.
    public sealed class OpcoesDeRevisao(IConfiguration configuracao) : IOpcoesDeRevisao
    {
        public bool Ligado => bool.TryParse(configuracao["Features:P2"], out var ligado) && ligado;

        public string? UrlModelo => configuracao["Review:UrlModelo"];

        public int PrazoMinutos => InteiroPositivo("Review:PrazoMinutos", 30);

        public int IntervaloConsultaSegundos => InteiroPositivo("Review:IntervaloConsultaSegundos", 15);

        // Valor ausente ou inválido cai no padrão; zero ou negativo não faz sentido para um prazo/intervalo.
        private int InteiroPositivo(string chave, int padrao)
            => int.TryParse(configuracao[chave], out var valor) && valor > 0 ? valor : padrao;
    }
}
