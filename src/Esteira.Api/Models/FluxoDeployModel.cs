using Newtonsoft.Json;

namespace Esteira.Api.Models
{
    public class FluxoDeployModel
    {
        [JsonProperty("sistema")]
        public string Sistema { get; set; }

        [JsonProperty("etapas")]
        public List<string> Etapas { get; set; }


    }
}
