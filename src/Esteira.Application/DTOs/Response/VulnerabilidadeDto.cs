using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class VulnerabilidadeDto
    {
        [JsonProperty("critica")]
        public int Critica { get; set; }

        [JsonProperty("alta")]
        public int Alta { get; set; }

        [JsonProperty("media")]
        public int Media { get; set; }

        [JsonProperty("baixa")]
        public int Baixa { get; set; }
    }
}
