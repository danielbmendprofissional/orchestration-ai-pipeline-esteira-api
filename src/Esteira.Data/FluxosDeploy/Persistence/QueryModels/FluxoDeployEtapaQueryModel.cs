using Esteira.Data.Etapas.Persistence.QueryModels;
using Esteira.Domain.Common.Enums;

namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployEtapaQueryModel
    {
        public int IdFluxoDeploy { get; private set; }
        public string SglEtapa { get; private set; }
        public int? Ordem { get; private set; }
        public StatusEnum Status { get; private set; }

        public EtapaQueryModel Etapa { get; private set; }
    }
}
