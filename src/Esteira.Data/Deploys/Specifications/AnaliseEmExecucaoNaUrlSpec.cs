using Esteira.Data.Common.Specifications;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Entities;

namespace Esteira.Data.Deploys.Specifications
{
    public sealed class AnaliseEmExecucaoNaUrlSpec : Specification<AnaliseEntity>
    {
        public AnaliseEmExecucaoNaUrlSpec(string urlAlvo)
        {
            Criteria = p => p.TipoAnalise == AnaliseEntity.TipoDinamica
                && p.Status == StatusEnum.EXECUTANDO
                && p.UrlAlvo == urlAlvo;
        }
    }
}
