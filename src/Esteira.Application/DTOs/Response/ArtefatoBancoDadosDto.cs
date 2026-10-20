using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class ArtefatoBancoDadosDto
    {
        public string Plano { get; set; }
        public string UrlCommit { get; set; }
        public DateTime DataCriacao { get; set; }
        public string Status { get; set; }
        public string Tipo { get; set; }
        public List<ScriptBancoDadosDto> Scripts { get; set; }
        
    }
}
