using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.DTOs.Response
{
    public class AprovacaoTecnicaDto
    {
        public string Usuario { get; set; }
        public DateTime? Execucao { get; set; }
        public char Aprovado { get; set; }
        public string Justificativa { get; set; }
        public string Equipe { get; set; }
        public IEnumerable<EquipeDto> Integrantes { get; set; }
    }
}
