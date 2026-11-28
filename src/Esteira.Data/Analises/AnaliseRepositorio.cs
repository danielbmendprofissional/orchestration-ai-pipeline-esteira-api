using Esteira.Application.Analises;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Analises
{
    // Escrita das Analises (com Achados) e da DecisaoDoGate no EsteiraContext. Quem grava é o IUnitOfWork, que traduz
    // índice único e concorrência em exceções técnicas da Application.
    public sealed class AnaliseRepositorio(EsteiraContext contexto) : IAnaliseRepositorio
    {
        public async Task SubstituirAsync(
            long fluxoId, AmbienteDeploy ambiente, EscopoAnalise escopo, long pipelineGitLabId,
            IReadOnlyList<AnaliseEntity> novas, CancellationToken cancellationToken = default)
        {
            // Os Achados saem junto (FK com cascade); o EF ordena os DELETEs antes dos INSERTs do mesmo índice único.
            var antigas = await contexto.Set<AnaliseEntity>()
                .Where(a => a.FluxoId == fluxoId && a.Ambiente == ambiente && a.Escopo == escopo && a.PipelineGitLabId == pipelineGitLabId)
                .ToListAsync(cancellationToken);
            contexto.Set<AnaliseEntity>().RemoveRange(antigas);
            await contexto.Set<AnaliseEntity>().AddRangeAsync(novas, cancellationToken);
        }

        public async Task RegistrarDecisaoAsync(
            long fluxoId, AmbienteDeploy ambiente, MomentoDoGate momento, AvaliacaoDoGate avaliacao,
            long pipelineGitLabId, DateTime em, CancellationToken cancellationToken = default)
        {
            var existente = await contexto.Set<DecisaoDoGateEntity>().FirstOrDefaultAsync(
                d => d.FluxoId == fluxoId && d.Ambiente == ambiente && d.Momento == momento && d.PipelineGitLabId == pipelineGitLabId,
                cancellationToken);
            if (existente is not null)
                existente.Sobrescrever(avaliacao, em);
            else
                await contexto.Set<DecisaoDoGateEntity>().AddAsync(DecisaoDoGateEntity.Criar(fluxoId, ambiente, momento, avaliacao, pipelineGitLabId, em), cancellationToken);
        }

        public void DescartarPendentes()
        {
            foreach (var entrada in contexto.ChangeTracker.Entries().ToList())
            {
                if (entrada.Entity is not (AnaliseEntity or AchadoEntity or DecisaoDoGateEntity))
                    continue;
                if (entrada.State == EntityState.Added)
                    entrada.State = EntityState.Detached;
                else if (entrada.State == EntityState.Deleted)
                    entrada.State = EntityState.Unchanged;
            }
        }
    }
}
