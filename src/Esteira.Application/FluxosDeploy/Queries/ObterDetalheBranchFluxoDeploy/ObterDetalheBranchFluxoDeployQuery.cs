using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Esteira.Application.FluxosDeploy.Queries.ObterDetalheBranchFluxoDeploy
{
    public class ObterDetalheBranchFluxoDeployQuery : IRequest<FluxoDeployCompletoDto>
    {
        public int Id { get; set; }
        public string Branch { get; set; }
    }
}
