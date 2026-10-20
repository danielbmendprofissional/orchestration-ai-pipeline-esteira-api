namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployGmudQueryModel
    {
        public int IdFluxoDeploy { get; private set; }
        public string Url { get; private set; }
        public string Justificativa { get; private set; }
        public string Usuario { get; private set; }
        public DateTime Execucao { get; private set; }
    }
}
