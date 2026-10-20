namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployVersaoTarefasQueryModel
    {
        public int IdFluxoDeploy { get; private set; }
        public int IdProjeto { get; private set; }
        public int IdVersao { get; private set; }
    }
}
