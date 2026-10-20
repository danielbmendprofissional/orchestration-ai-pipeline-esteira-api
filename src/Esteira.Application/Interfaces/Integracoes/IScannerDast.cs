using Esteira.Application.DTOs.Response;

namespace Esteira.Application.Interfaces.Integracoes
{
    // Porta para a ferramenta de análise dinâmica (DAST). Sem ferramenta definida: implementar um adaptador
    // (ex.: OWASP ZAP) em Esteira.AcL e registrar no IoC no lugar de ScannerDastNaoConfigurado.
    public interface IScannerDast
    {
        Task<string> ExecutarAnalise(string urlAlvo, List<string> emailNotificacao);
        Task<IEnumerable<ArtefatoCompletoDto>> ObterArtefatosAnalise(string scanId, Action<string> setStatus);
    }
}
