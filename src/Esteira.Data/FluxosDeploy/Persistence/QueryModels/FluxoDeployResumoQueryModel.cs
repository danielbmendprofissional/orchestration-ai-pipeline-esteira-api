using Esteira.Domain.Common.Enums;

namespace Esteira.Data.FluxosDeploy.Persistence.QueryModels
{
    public sealed class FluxoDeployResumoQueryModel
    {
        public int IdFluxoDeploy { get; private set; }
        public StatusEnum Status { get; private set; }
        public DateTime? Inicio { get; private set; }
        public DateTime? Fim { get; private set; }
        public string UsuarioCriacao { get; private set; }
        public string Sistema { get; private set; }
        public string Versao { get; private set; }
        public string Linguagens { get; private set; }
        public string ProjetosGit { get; private set; }
        public int VulnerabilidadeCritica { get; private set; }
        public int VulnerabilidadeAlta { get; private set; }
        public int VulnerabilidadeMedia { get; private set; }
        public int VulnerabilidadeBaixa { get; private set; }
        public string TarefasNegocio { get; private set; }
        public string AprovacaoTecnica { get; private set; }
        public string Crs { get; private set; }
        public string Etapas { get; private set; }
        public string Gmuds { get; private set; }
        public string VersoesTarefas { get; private set; }
    }
}
