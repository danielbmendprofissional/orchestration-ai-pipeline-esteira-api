using MediatR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Request
{
    public class BdCrsDtoRequest
    {
        [JsonProperty("idCrs")]
        public string IdCrs { get; set; }

        [JsonProperty("crs")]
        public string Crs { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
