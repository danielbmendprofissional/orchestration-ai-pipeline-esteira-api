namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployBdCrsQueryModel
    {
        public int Id { get; private set; }
        public int IdFluxoDeploy { get; private set; }
        public string Crs { get; private set; }
        public string Url { get; private set; }
    }
}
