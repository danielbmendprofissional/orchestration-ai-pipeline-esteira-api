using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class FluxoDeployResumoDto
    {
        public int IdFluxoDeploy { get; set; }
        public string Status { get; set; }
        public DateTime? Inicio { get; set; }
        public DateTime? Fim { get; set; }
        public string UsuarioCriacao { get; set; }
        public string Sistema { get; set; }
        public string Versao { get; set; }

        public string Linguagens { get; set; }
        public string ProjetosGit { get; set; }

        public int VulnerabilidadeCritica { get; set; }
        public int VulnerabilidadeAlta { get; set; }
        public int VulnerabilidadeMedia { get; set; }
        public int VulnerabilidadeBaixa { get; set; }

        public string TarefasNegocio { get; set; }
        public string AprovacaoTecnica { get; set; }
        public string Crs { get; set; }
        public string Etapas { get; set; }
        public string Gmuds { get; set; }
        public string VersoesTarefas { get; set; }
    }
}
