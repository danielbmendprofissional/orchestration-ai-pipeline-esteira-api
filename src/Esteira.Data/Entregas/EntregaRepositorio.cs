using Esteira.Data.Common.Persistence;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    public sealed class EntregaRepositorio(EsteiraContext contexto) : IEntregaRepositorio
    {
        public async Task AdicionarAsync(EntregaEntity entrega, CancellationToken cancellationToken = default)
            => await contexto.Set<EntregaEntity>().AddAsync(entrega, cancellationToken);

        public Task<EntregaEntity?> ObterPorPublicIdAsync(Guid entregaPublicId, CancellationToken cancellationToken = default)
            => contexto.Set<EntregaEntity>()
                .Include(e => e.Fluxos).ThenInclude(f => f.Fatias)
                .Include(e => e.Fluxos).ThenInclude(f => f.Etapas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.PublicId == entregaPublicId, cancellationToken);

        public Task<FluxoEntity?> ObterFluxoPorPublicIdAsync(Guid fluxoPublicId, CancellationToken cancellationToken = default)
            => contexto.Set<FluxoEntity>()
                .Include(f => f.Fatias)
                .Include(f => f.Etapas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(f => f.PublicId == fluxoPublicId, cancellationToken);

        public Task<string?> ObterShaGravadoDoFluxoAsync(long fluxoId, CancellationToken cancellationToken = default)
            => contexto.Set<FluxoEntity>().AsNoTracking()
                .Where(f => f.Id == fluxoId)
                .Select(f => f.Sha)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<EtapaDaPipeline?> ObterEtapaPorPipelineAsync(long projetoId, long pipelineGitLabId, CancellationToken cancellationToken = default)
        {
            var etapas = contexto.Set<EtapaFluxoEntity>();
            var fluxos = contexto.Set<FluxoEntity>().Where(f => f.ProjetoId == projetoId);
            var achada = await etapas
                .Where(e => e.PipelineGitLabId == pipelineGitLabId)
                .Join(fluxos, e => e.FluxoId, f => f.Id, (e, f) => new { Etapa = e, FluxoPublicId = f.PublicId, f.EntregaId })
                .OrderBy(x => x.Etapa.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (achada is null)
                return null;

            var entregaPublicId = await contexto.Set<EntregaEntity>().AsNoTracking()
                .Where(e => e.Id == achada.EntregaId).Select(e => e.PublicId).FirstAsync(cancellationToken);
            return new EtapaDaPipeline(achada.Etapa, achada.FluxoPublicId, entregaPublicId);
        }

        public Task RecarregarAsync(EtapaFluxoEntity etapa, CancellationToken cancellationToken = default)
            => contexto.Entry(etapa).ReloadAsync(cancellationToken);
    }
}
