using Esteira.Data.AprovadoresTecnicos.Persistence.QueryModels;

namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployAprovadorTecnicoQueryModel
    {
        public int IdFluxoDeploy { get; private set; }
        public int IdAprovadorTecnico { get; private set; }
        public DateTime? Execucao { get; private set; }
        public bool Aprovado { get; private set; }
        public string Justificativa { get; private set; }

        public AprovadorTecnicoQueryModel AprovadorTecnico { get; private set; }
    }
}
