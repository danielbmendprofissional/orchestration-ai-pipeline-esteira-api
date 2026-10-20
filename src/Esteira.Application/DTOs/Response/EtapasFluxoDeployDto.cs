using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class EtapasFluxoDeployDto
    {
        public string SglEtapa { get; set; }
        public int? Ordem { get; set; }
        public string Status { get; set; }
        public string Descricao { get; set; }
    }
}
