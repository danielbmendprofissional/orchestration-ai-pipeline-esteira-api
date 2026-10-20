using Esteira.Application.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Messages
{
    public class CriarAnaliseArtefatosSastMessage
    {   
        public List<ArtefatoCompletoDto> Artefato { get; set; }
        public string Ferramenta { get; set; }
        public int DeployId { get; set; }
        public int IdPipeline { get; set; }
        public string Tag { get; set; }
    }
}
