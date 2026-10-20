using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Messages
{
    public class AtualizarAnaliseDastMessage
    {
        public string ScanId { get; set; }
        public int IdAnalise { get; set; }        
    }
}
