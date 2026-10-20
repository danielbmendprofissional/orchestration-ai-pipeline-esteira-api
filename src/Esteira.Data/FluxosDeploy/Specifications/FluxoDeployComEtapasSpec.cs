using Esteira.Data.Common.Specifications;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.FluxosDeploy.Specifications
{
    public sealed class FluxoDeployComEtapasSpec : Specification<FluxoDeployQueryModel>
    {
        public FluxoDeployComEtapasSpec(int idFluxoDeploy)
        {
            Criteria = p => p.Id == idFluxoDeploy;

            Includes.Add(query => query
                .Include(p => p.Etapas)
                    .ThenInclude(e => e.Etapa));
        }
    }
}
