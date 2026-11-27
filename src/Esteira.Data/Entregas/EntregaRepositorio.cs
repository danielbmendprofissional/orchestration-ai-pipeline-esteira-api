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
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.PublicId == entregaPublicId, cancellationToken);

        public Task<FluxoEntity?> ObterFluxoPorPublicIdAsync(Guid fluxoPublicId, CancellationToken cancellationToken = default)
            => contexto.Set<FluxoEntity>()
                .Include(f => f.Fatias)
                .FirstOrDefaultAsync(f => f.PublicId == fluxoPublicId, cancellationToken);

        public Task<string?> ObterShaGravadoDoFluxoAsync(long fluxoId, CancellationToken cancellationToken = default)
            => contexto.Set<FluxoEntity>().AsNoTracking()
                .Where(f => f.Id == fluxoId)
                .Select(f => f.Sha)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
