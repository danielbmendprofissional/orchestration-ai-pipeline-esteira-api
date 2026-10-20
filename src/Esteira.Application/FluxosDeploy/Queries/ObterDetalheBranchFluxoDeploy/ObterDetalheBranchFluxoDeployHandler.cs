using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterDetalheBranchFluxoDeploy
{
    public class ObterDetalheBranchFluxoDeployHandler : IRequestHandler<ObterDetalheBranchFluxoDeployQuery, FluxoDeployCompletoDto>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterDetalheBranchFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<FluxoDeployCompletoDto> Handle(
            ObterDetalheBranchFluxoDeployQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ObterDetalheBranchAsync(request.Id, request.Branch, cancellationToken);
    }
}
