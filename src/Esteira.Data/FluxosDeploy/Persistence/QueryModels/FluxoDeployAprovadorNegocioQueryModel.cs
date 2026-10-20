namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployAprovadorNegocioQueryModel
    {
        public int Id { get; private set; }
        public int IdFluxoDeploy { get; private set; }
        public int IdTarefa { get; private set; }
        public string Url { get; private set; }
    }
}
