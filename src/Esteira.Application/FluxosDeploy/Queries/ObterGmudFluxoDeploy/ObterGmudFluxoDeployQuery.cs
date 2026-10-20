using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Queries.ObterGmudFluxoDeploy
{
    public class ObterGmudFluxoDeployQuery: IRequest<GmudDto>
    {
        public ObterGmudFluxoDeployQuery(int idFluxoDeploy) 
        { 
            IdFluxoDeploy = idFluxoDeploy;
        }
        public int IdFluxoDeploy { get; set; }
    }
}
