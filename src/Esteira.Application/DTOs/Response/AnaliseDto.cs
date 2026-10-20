using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class AnaliseDto
    {
        [JsonProperty("idAnalise", NullValueHandling = NullValueHandling.Ignore)]
        public int? IdAnalise { get; set; }

        [JsonProperty("pipeline", NullValueHandling = NullValueHandling.Ignore)]
        public int? IdPipeline { get; set; }

        [JsonProperty("ferramenta", NullValueHandling = NullValueHandling.Ignore)]
        public string Ferramenta { get; set; }

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string Tag { get; set; }

        [JsonProperty("tipoAnalise", NullValueHandling = NullValueHandling.Ignore)]
        public string TipoAnalise { get; set; }


        [JsonProperty("status")]
        public string Status { get; set; }
        
        [JsonProperty("vulnerabilidades", NullValueHandling = NullValueHandling.Ignore)]
        public VulnerabilidadeDto Vulnerabilidades { get; set; }
    }
}
