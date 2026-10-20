using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Data.Common.Specifications;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;

namespace Esteira.Data.FluxosDeploy.Specifications
{
    public sealed class FluxosDeployPorFiltroSpec : Specification<FluxoDeployQueryModel>
    {
        public FluxosDeployPorFiltroSpec(FiltroFluxoDeploy filtro)
        {
            var sistema = string.IsNullOrWhiteSpace(filtro.Sistema) ? null : filtro.Sistema.ToUpper();
            DateTime? inicio = filtro.DataInicio?.ToDateTime(TimeOnly.MinValue);
            DateTime? fim = filtro.DataFim?.ToDateTime(TimeOnly.MaxValue);
            var status = filtro.Status;

            Criteria = p =>
                (sistema == null || p.Sistema.ToUpper() == sistema) &&
                (inicio == null || p.Inicio >= inicio) &&
                (fim == null || p.Fim <= fim) &&
                (status == null || p.Status == status);
        }
    }
}
