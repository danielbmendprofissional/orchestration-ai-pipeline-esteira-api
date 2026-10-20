using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Repositories;
using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Specifications;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Deploys.Repositories
{
    public sealed class DeployRepository : RepositoryBase<DeployEntity>, IDeployRepository
    {
        public DeployRepository(EsteiraContext context) : base(context)
        {
        }

        private IQueryable<DeployEntity> Agregado
            => Context.Deploys.Include(a => a.Analises);

        public async Task<DeployEntity> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => (await Agregado.Where(a => a.Id == id).ToListAsync(cancellationToken)).FirstOrDefault();

        public async Task<DeployEntity> ObterPorAnaliseAsync(int idAnalise, CancellationToken cancellationToken = default)
        {
            var deploy = (await Agregado.Where(a => a.Analises.Any(b => b.Id == idAnalise)).ToListAsync(cancellationToken)).FirstOrDefault();

            if (deploy is null)
                return null;

            var analise = deploy.Analises.Single(a => a.Id == idAnalise);

            await Context.Entry(analise)
                .Collection(a => a.Artefatos)
                .LoadAsync(cancellationToken);

            return deploy;
        }

        public Task<bool> ExisteAnaliseDinamicaEmExecucaoNaUrlAsync(string urlAlvo, CancellationToken cancellationToken = default)
            => Context.Set<AnaliseEntity>().ExisteAsync(new AnaliseEmExecucaoNaUrlSpec(urlAlvo), cancellationToken);

        public async Task<IReadOnlyList<DeployEntity>> ObterPorFluxosAsync(IEnumerable<int> idsFluxoDeploy, CancellationToken cancellationToken = default)
        {
            var ids = idsFluxoDeploy.ToList();

            return await Agregado
                .Where(a => ids.Contains(a.IdFluxoDeploy))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DeployEntity>> ObterPorFluxoEStatusAsync(int idFluxoDeploy, StatusEnum status, CancellationToken cancellationToken = default)
            => await Agregado
                .Where(a => a.IdFluxoDeploy == idFluxoDeploy && a.Status == status)
                .ToListAsync(cancellationToken);
    }
}
