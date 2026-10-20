using Esteira.Application.DTOs.Response;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterResumoFluxoDeploy
{
    public class ObterResumoFluxoDeployQuery : IRequest<FluxoDeployResumoDto>
    {
        public int Id { get; set; }
    }
}