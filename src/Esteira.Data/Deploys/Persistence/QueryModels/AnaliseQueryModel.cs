using Esteira.Domain.Common.Enums;

namespace Esteira.Data.Deploys.Persistence.QueryModels
{
    public sealed class AnaliseQueryModel
    {
        public int Id { get; private set; }
        public int IdDeploy { get; private set; }
        public string Ferramenta { get; private set; }
        public string TipoAnalise { get; private set; }
        public int IdPipeline { get; private set; }
        public string Tag { get; private set; }
        public string ScanId { get; private set; }
        public StatusEnum Status { get; private set; }
        public int VulnerabilidadeCritica { get; private set; }
        public int VulnerabilidadeAlta { get; private set; }
        public int VulnerabilidadeMedia { get; private set; }
        public int VulnerabilidadeBaixa { get; private set; }
    }
}
