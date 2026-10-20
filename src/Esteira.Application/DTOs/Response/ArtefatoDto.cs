using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class ArtefatoDto
    {   
        public int Id { get; set; }
        public int IdAnalise { get; set; }
        public string Severidade { get; set; }
        public string TituloVulnerabilidade { get; set; }
        public string LocalArquivo { get; set; }
        public string Ferramenta { get; set; }
       
    }


}
