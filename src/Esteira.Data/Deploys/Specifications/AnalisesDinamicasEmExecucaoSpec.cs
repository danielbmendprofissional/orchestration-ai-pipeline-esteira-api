using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Entities;

namespace Esteira.Data.Deploys.Specifications
{
    public sealed class AnalisesDinamicasEmExecucaoSpec : Specification<AnaliseQueryModel>
    {
        public AnalisesDinamicasEmExecucaoSpec()
        {
            Criteria = p => p.TipoAnalise == AnaliseEntity.TipoDinamica && p.Status == StatusEnum.EXECUTANDO && p.ScanId != null;
        }
    }
}
