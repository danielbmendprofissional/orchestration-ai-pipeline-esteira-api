using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.Deploys.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Deploys.Specifications
{
    public sealed class DeploysPorFluxoEBranchSpec : Specification<DeployQueryModel>
    {
        public DeploysPorFluxoEBranchSpec(int idFluxoDeploy, string branch)
        {
            var branchNormalizada = StringValueObject.Normalize(branch);
            var statusConsiderados = AnaliseEntity.StatusConsiderados;

            Criteria = p => p.IdFluxoDeploy == idFluxoDeploy && p.Branch == branchNormalizada;

            Includes.Add(query => query
                .Include(p => p.Analises.Where(a => statusConsiderados.Contains(a.Status))));
        }
    }
}
