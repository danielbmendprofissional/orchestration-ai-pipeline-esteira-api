using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class ArtefatoCompletoDto : ArtefatoDto
    {   
        
        public List<string> Referencias { get; set; }
        public string DescricaoVulnerabilidade { get; set; }
        public List<string> CatalogoCWE { get; set; }
        public List<string> CatalogoOWASP { get; set; }
        public List<string> CatalogoCVE { get; set; }
        public string PacoteCorrecao { get; set; }
        public string Linha { get; set; }

    }
}
