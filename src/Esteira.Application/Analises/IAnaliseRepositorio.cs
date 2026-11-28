using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Analises
{
    // Escrita das Analises e da DecisaoDoGate (EsteiraContext). O SaveChanges é do IUnitOfWork.
    public interface IAnaliseRepositorio
    {
        // Reenvio do mesmo job: remove as Analises (e os Achados) do mesmo Fluxo, Ambiente, escopo e pipeline e grava as novas.
        Task SubstituirAsync(
            long fluxoId, AmbienteDeploy ambiente, EscopoAnalise escopo, long pipelineGitLabId,
            IReadOnlyList<AnaliseEntity> novas, CancellationToken cancellationToken = default);

        // Cria a decisão ou sobrescreve a do mesmo Fluxo + Ambiente + Momento + pipeline.
        Task RegistrarDecisaoAsync(
            long fluxoId, AmbienteDeploy ambiente, MomentoDoGate momento, AvaliacaoDoGate avaliacao,
            long pipelineGitLabId, DateTime em, CancellationToken cancellationToken = default);

        // Depois de um conflito de gravação: solta o que esta chamada deixou pendente para regravar do zero.
        void DescartarPendentes();
    }
}
