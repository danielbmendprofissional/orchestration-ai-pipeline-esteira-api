using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Repositories;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.AprovadoresTecnicos.Repositories
{
    public sealed class AprovadorTecnicoRepository : RepositoryBase<AprovadorTecnicoEntity>, IAprovadorTecnicoRepository
    {
        public AprovadorTecnicoRepository(EsteiraContext context) : base(context)
        {
        }

        public async Task<AprovadorTecnicoEntity> GetByUsuarioAsync(string usuario, CancellationToken cancellationToken = default)
            => await Context.AprovadoresTecnicos.FirstOrDefaultAsync(a => a.Usuario == usuario, cancellationToken);

        public async Task<IReadOnlyList<AprovadorTecnicoEntity>> ObterPorIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
        {
            var lista = ids.ToList();

            return await Context.AprovadoresTecnicos
                .Where(a => lista.Contains(a.Id))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AprovadorTecnicoEntity>> ObterPorEquipeAsync(string equipe, CancellationToken cancellationToken = default)
            => await Context.AprovadoresTecnicos
                .Where(a => a.Equipe == equipe)
                .ToListAsync(cancellationToken);

        public async Task<List<string>> ObterEquipesTecnicasAsync(CancellationToken cancellationToken = default)
            => await Context.AprovadoresTecnicos
                .Select(a => a.Equipe)
                .Distinct()
                .ToListAsync(cancellationToken);
    }
}
