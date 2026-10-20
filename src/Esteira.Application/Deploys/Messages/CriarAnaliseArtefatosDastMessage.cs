using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Messages
{
    public class CriarAnaliseArtefatosDastMessage
    {
        public int DeployId { get; set; }
        public string Ferramenta { get; set; }
        public int IdPipeline { get; set; }
        public string Tag { get; set; }
        public string Url { get; set; } 
        public List<string> EmailsNotificacao { get; set; }
    }
}
