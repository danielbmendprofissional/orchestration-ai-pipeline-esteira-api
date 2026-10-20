using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Repositories;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.FluxosDeploy.Repositories
{
    public sealed class FluxoDeployRepository : RepositoryBase<FluxoDeployEntity>, IFluxoDeployRepository
    {
        public FluxoDeployRepository(EsteiraContext context) : base(context)
        {
        }

        private IQueryable<FluxoDeployEntity> Agregado
            => Context.FluxosDeploy
                .Include(a => a.Etapas)
                .Include(a => a.Bds)
                .Include(a => a.AprovadoresNegocio)
                .Include(a => a.AprovadoresTecnicos)
                .Include(a => a.Gmud)
                .Include(a => a.VersaoTarefas)
                .AsSplitQuery();

        public async Task<FluxoDeployEntity> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => (await Agregado.Where(a => a.Id == id).ToListAsync(cancellationToken)).FirstOrDefault();

        public async Task<IReadOnlyList<FluxoDeployEntity>> ObterPorSistemaSomenteEtapasAsync(string sistema, StatusEnum status, CancellationToken cancellationToken = default)
            => await Context.FluxosDeploy
                .AsNoTracking()
                .Include(a => a.Etapas)
                .Where(a => a.Sistema == sistema && a.Status == status)
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

        public async Task<string> ObterUltimaVersaoSistema(string sistema, CancellationToken cancellationToken = default)
            => await Context.FluxosDeploy
                .Where(a => a.Sistema == sistema)
                .OrderByDescending(a => a.Inicio)
                .Select(a => a.Versao)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
