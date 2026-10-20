using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class EtapaDto
    {
        [JsonProperty("sigla")]
        public string Sigla { get; set; }

        [JsonProperty("descricao")]
        public string Descricao { get; set; }

        [JsonProperty("ordem")]
        public int Ordem { get; set; }

        [JsonProperty("obrigatorio")]
        public char Obrigatorio { get; set; }
    }
}
