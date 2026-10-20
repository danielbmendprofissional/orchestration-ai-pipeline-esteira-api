using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response.Common
{
    public class BaseMessageResponseDto
    {
        public BaseMessageResponseDto(string message) 
        { 
            Message = message;
        }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
