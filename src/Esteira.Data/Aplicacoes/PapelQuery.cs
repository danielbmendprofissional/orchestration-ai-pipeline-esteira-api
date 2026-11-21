using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class PapelQuery(EsteiraReadContext contexto) : IPapelQuery
    {
        public async Task<IReadOnlyList<PapelResumoDto>> ListarAsync(long aplicacaoId, CancellationToken cancellationToken = default)
        {
            var papeis = await contexto.Set<PapelAplicacaoEntity>().Where(p => p.AplicacaoId == aplicacaoId).OrderBy(p => p.Id).ToListAsync(cancellationToken);
            return await ComPessoasAsync(contexto, papeis, cancellationToken);
        }

        // Junta o papel com a Pessoa pelo `sub`; sem Pessoa (nunca entrou ou removida), nome e e-mail ficam nulos.
        internal static async Task<List<PapelResumoDto>> ComPessoasAsync(
            EsteiraReadContext contexto, IReadOnlyList<PapelAplicacaoEntity> papeis, CancellationToken cancellationToken)
        {
            var subs = papeis.Select(p => p.Usuario).Distinct().ToList();
            var pessoas = subs.Count == 0
                ? new Dictionary<string, PessoaEntity>()
                : await contexto.Set<PessoaEntity>().Where(p => subs.Contains(p.Sub)).ToDictionaryAsync(p => p.Sub, cancellationToken);

            return papeis.Select(p =>
            {
                pessoas.TryGetValue(p.Usuario, out var pessoa);
                return new PapelResumoDto
                {
                    Id = p.PublicId,
                    Usuario = p.Usuario,
                    Nome = pessoa?.Nome,
                    Email = pessoa?.Email,
                    Papel = EnumKebab.Texto(p.Papel)
                };
            }).ToList();
        }
    }
}
