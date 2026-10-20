using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterEtapasFluxoDeploy
{
    public class ObterEtapasFluxoDeployHandler : IRequestHandler<ObterEtapasFluxoDeployQuery, FluxoDeployDto>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterEtapasFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<FluxoDeployDto> Handle(
            ObterEtapasFluxoDeployQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ObterComEtapasAsync(request.IdFluxoDeploy, cancellationToken);
    }
}
