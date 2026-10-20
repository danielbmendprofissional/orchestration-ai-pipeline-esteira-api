using Newtonsoft.Json;

namespace Esteira.Api.Models
{
    public class EtapaModel
    {
        [JsonProperty("sigla")]
        public string Sigla { get; set; }

        [JsonProperty("descricao")]
        public string Descricao { get; set; }

        [JsonProperty("obrigatorio")]
        public string Obrigatorio { get; set; }
    }
}
