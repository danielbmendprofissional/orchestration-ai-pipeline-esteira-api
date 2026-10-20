using Esteira.Domain.Common.Enums;

namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployQueryModel
    {
        public int Id { get; private set; }
        public StatusEnum Status { get; private set; }
        public DateTime Inicio { get; private set; }
        public DateTime? Fim { get; private set; }
        public string Sistema { get; private set; }
        public string Usuario { get; private set; }
        public string Versao { get; private set; }

        public ICollection<FluxoDeployEtapaQueryModel> Etapas { get; private set; } = new List<FluxoDeployEtapaQueryModel>();
    }
}
