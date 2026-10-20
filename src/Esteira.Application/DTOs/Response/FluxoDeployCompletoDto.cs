using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class FluxoDeployCompletoDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("branch")]
        public string Branch { get; set; }

        [JsonProperty("vulnerabilidadesTotais", NullValueHandling = NullValueHandling.Ignore)]
        public VulnerabilidadeDto Vulnerabilidades { get; set; }

        [JsonProperty("deploys")]
        public List<DeployDto> Deploys { get; set; } = new List<DeployDto>();
    }
}
