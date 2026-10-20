using Esteira.Application.Interfaces.Cache;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Repositories;
using Esteira.Data.Common.Specifications;
using Esteira.Data.Etapas.Specifications;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Etapas.Repositories
{
    public sealed class EtapaRepository : RepositoryBase<EtapaEntity>, IEtapaRepository
    {
        private static readonly TimeSpan DuracaoCatalogo = TimeSpan.FromMinutes(10);

        private readonly ICacheService _cache;

        public EtapaRepository(EsteiraContext context, ICacheService cache) : base(context)
        {
            _cache = cache;
        }

        public async Task<EtapaEntity> GetBySiglaAsync(string sigla, CancellationToken cancellationToken = default)
            => await Context.Etapas.FirstOrDefaultAsync(a => a.Sigla == sigla, cancellationToken);

        public Task<bool> ExisteSiglaAsync(string sigla, CancellationToken cancellationToken = default)
            => Context.Etapas.ExisteAsync(new EtapaPorSiglaSpec(sigla), cancellationToken);

        public async Task<IReadOnlyList<EtapaEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => await Context.Etapas.ToListAsync(cancellationToken);

        public Task<IReadOnlyList<EtapaEntity>> ObterCatalogoAsync(CancellationToken cancellationToken = default)
            => _cache.ObterOuCriarAsync<IReadOnlyList<EtapaEntity>>(
                ChavesCache.EtapasCatalogo,
                async () => await Context.Etapas.AsNoTracking().ToListAsync(cancellationToken),
                DuracaoCatalogo);

        public async Task<EtapaEntity> ObterUltimaEtapa(CancellationToken cancellationToken = default)
            => await Context.Etapas
                .OrderByDescending(a => a.Ordem)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
