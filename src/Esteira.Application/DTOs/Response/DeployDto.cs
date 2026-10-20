using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class DeployDto
    {
        [JsonProperty("idDeploy")]
        public int IdDeploy { get; set; }

        [JsonProperty("idProjetoGit")]
        public int IdProjetoGit { get; set; }

        [JsonProperty("nomeProjetoGit")]
        public string NomeProjetoGit { get; set; }

        [JsonProperty("liguagem")]
        public string Linguagem { get; set; }

        [JsonProperty("versao")]
        public string VersaoAplicacao { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("analises")]
        public List<AnaliseDto> Analises { get; set; }
    }
}
