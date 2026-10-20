using Esteira.Data.Common.Specifications;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.FluxosDeploy.Specifications
{
    public sealed class AprovacoesTecnicasPorFluxoDeploySpec : Specification<FluxoDeployAprovadorTecnicoQueryModel>
    {
        public AprovacoesTecnicasPorFluxoDeploySpec(int idFluxoDeploy)
        {
            Criteria = p => p.IdFluxoDeploy == idFluxoDeploy;

            Includes.Add(query => query.Include(p => p.AprovadorTecnico));
        }
    }
}
