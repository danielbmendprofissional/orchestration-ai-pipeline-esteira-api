using Esteira.Data.Common.Specifications;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;

namespace Esteira.Data.FluxosDeploy.Specifications
{
    public sealed class FluxoDeployPorIdSpec : Specification<FluxoDeployQueryModel>
    {
        public FluxoDeployPorIdSpec(int idFluxoDeploy)
        {
            Criteria = p => p.Id == idFluxoDeploy;
        }
    }
}
