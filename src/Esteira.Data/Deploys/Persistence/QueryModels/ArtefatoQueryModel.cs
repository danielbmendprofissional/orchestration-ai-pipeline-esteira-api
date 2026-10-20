namespace Esteira.Data.Deploys.Persistence.QueryModels
{
    public sealed class ArtefatoQueryModel
    {
        public int Id { get; private set; }
        public int IdAnalise { get; private set; }
        public string Severidade { get; private set; }
        public string LocalArquivo { get; private set; }
        public string TituloVulnerabilidade { get; private set; }
        public string DescricaoVulnerabilidade { get; private set; }
        public string PacoteCorrecao { get; private set; }
        public string Linha { get; private set; }
        public List<string> Referencias { get; private set; }
        public List<string> CatalogoCWE { get; private set; }
        public List<string> CatalogoOWASP { get; private set; }
        public List<string> CatalogoCVE { get; private set; }

        public AnaliseQueryModel Analise { get; private set; }
    }
}
