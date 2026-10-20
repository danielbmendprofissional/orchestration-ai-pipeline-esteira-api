using Esteira.Data.Common.Specifications;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;

namespace Esteira.Data.FluxosDeploy.Specifications
{
    public sealed class CrsPorFluxoDeploySpec : Specification<FluxoDeployBdCrsQueryModel>
    {
        public CrsPorFluxoDeploySpec(int idFluxoDeploy)
        {
            Criteria = p => p.IdFluxoDeploy == idFluxoDeploy;
        }
    }
}
