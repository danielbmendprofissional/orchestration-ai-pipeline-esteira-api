using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Queries.ObterAprovacoesTecnicasFluxoDeploy
{
    public class ObterAprovacoesTecnicasFluxoDeployQuery: IRequest<IReadOnlyList<AprovacaoTecnicaDto>>
    {
        public int IdFluxoDeploy { get;set; }
    }
}
