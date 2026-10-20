using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Esteira.Domain.Deploys.Entities;

namespace Esteira.Data.Deploys.Specifications
{
    public sealed class ArtefatosPorDeploySpec : Specification<ArtefatoQueryModel>
    {
        public ArtefatosPorDeploySpec(int idDeploy)
        {
            var statusConsiderados = AnaliseEntity.StatusConsiderados;

            Criteria = p => p.Analise.IdDeploy == idDeploy && statusConsiderados.Contains(p.Analise.Status);
        }
    }
}
