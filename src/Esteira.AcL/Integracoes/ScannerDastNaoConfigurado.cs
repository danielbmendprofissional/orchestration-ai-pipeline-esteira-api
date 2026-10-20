using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Exceptions;

namespace Esteira.AcL.Integracoes
{
    public sealed class ScannerDastNaoConfigurado : IScannerDast
    {
        private const string Mensagem = "Nenhuma ferramenta de análise dinâmica (DAST) está configurada.";

        public Task<string> ExecutarAnalise(string urlAlvo, List<string> emailNotificacao)
            => throw new IntegrationException(Mensagem, 501);

        public Task<IEnumerable<ArtefatoCompletoDto>> ObterArtefatosAnalise(string scanId, Action<string> setStatus)
            => throw new IntegrationException(Mensagem, 501);
    }
}
