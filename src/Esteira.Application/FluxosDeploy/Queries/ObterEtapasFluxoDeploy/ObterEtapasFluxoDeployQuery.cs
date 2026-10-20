using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Queries.ObterEtapasFluxoDeploy
{
    public class ObterEtapasFluxoDeployQuery: IRequest<FluxoDeployDto>
    {
        public int IdFluxoDeploy { get; set; }
    }
}
