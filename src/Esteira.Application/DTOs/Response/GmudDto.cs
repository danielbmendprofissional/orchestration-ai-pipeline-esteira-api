using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class GmudDto
    {
        [JsonProperty("url")]
        public string Url { get; set; }
        
        [JsonProperty("justificativa", NullValueHandling = NullValueHandling.Ignore)]
        public string Justificativa { get; set; }
        
        [JsonProperty("usuario", NullValueHandling = NullValueHandling.Ignore)]
        public string Usuario { get; set; }
        
        [JsonProperty("execucao", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Execucao { get; set; }
        
    }

    
}
