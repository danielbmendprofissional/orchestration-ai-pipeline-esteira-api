using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.AprovadoresTecnicos.Queries.ObterEquipeAprovacaoTecnica
{
    public class ObterEquipeAprovacaoTecnicaQuery: IRequest<IReadOnlyList<EquipeDto>>
    {  
        public string Equipe { get; set; }
    }
}
