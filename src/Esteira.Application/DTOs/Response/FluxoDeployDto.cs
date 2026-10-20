using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class FluxoDeployDto
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime? Fim { get; set; }
        public string Sistema { get; set; }
        public string Versao { get; set; }
        public string Usuario { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<EtapasFluxoDeployDto> Etapas { get; set; }
    }
}
