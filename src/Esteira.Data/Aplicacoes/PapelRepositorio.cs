using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class PapelRepositorio(EsteiraContext contexto) : IPapelRepositorio
    {
        public Task<bool> ExisteAsync(long aplicacaoId, string usuario, PapelAplicacao papel, CancellationToken cancellationToken = default)
            => contexto.Set<PapelAplicacaoEntity>().AnyAsync(p => p.AplicacaoId == aplicacaoId && p.Usuario == usuario && p.Papel == papel, cancellationToken);

        public Task<PapelAplicacaoEntity?> ObterAsync(long aplicacaoId, Guid publicId, CancellationToken cancellationToken = default)
            => contexto.Set<PapelAplicacaoEntity>().FirstOrDefaultAsync(p => p.AplicacaoId == aplicacaoId && p.PublicId == publicId, cancellationToken);

        public async Task AdicionarAsync(PapelAplicacaoEntity papel, CancellationToken cancellationToken = default)
            => await contexto.Set<PapelAplicacaoEntity>().AddAsync(papel, cancellationToken);

        public void Remover(PapelAplicacaoEntity papel)
            => contexto.Set<PapelAplicacaoEntity>().Remove(papel);
    }
}
