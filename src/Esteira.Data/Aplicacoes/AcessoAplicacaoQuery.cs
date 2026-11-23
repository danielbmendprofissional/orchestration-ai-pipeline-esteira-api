using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class AcessoAplicacaoQuery(EsteiraReadContext contexto) : IAcessoAplicacaoQuery
    {
        public async Task<long?> ResolverIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var id = await contexto.Set<AplicacaoEntity>()
                .Where(a => a.PublicId == publicId)
                .Select(a => (long?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return id;
        }

        public async Task<Guid?> ObterPublicIdAsync(long aplicacaoId, CancellationToken cancellationToken = default)
            => await contexto.Set<AplicacaoEntity>()
                .Where(a => a.Id == aplicacaoId)
                .Select(a => (Guid?)a.PublicId)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<PapelAplicacao>> PapeisAsync(long aplicacaoId, string sub, CancellationToken cancellationToken = default)
            => await contexto.Set<PapelAplicacaoEntity>()
                .Where(p => p.AplicacaoId == aplicacaoId && p.Usuario == sub)
                .Select(p => p.Papel)
                .ToListAsync(cancellationToken);
    }
}
