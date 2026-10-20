using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Request
{
    public class TarefaDtoRequest
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public string Autor { get; set; }
        public string Titulo { get; set; }
        public string Tipo { get; set; }
        public string Url { get; set; }
        public bool AprovacaoNegocio { get; set; }
    }
}
