using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterAprovacoesTecnicasFluxoDeploy
{
    public class ObterAprovacoesTecnicasFluxoDeployHandler : IRequestHandler<ObterAprovacoesTecnicasFluxoDeployQuery, IReadOnlyList<AprovacaoTecnicaDto>>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterAprovacoesTecnicasFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<IReadOnlyList<AprovacaoTecnicaDto>> Handle(
            ObterAprovacoesTecnicasFluxoDeployQuery request,
            CancellationToken cancellationToken)
            => await _queryRepository.ObterAprovacoesTecnicasAsync(request.IdFluxoDeploy, cancellationToken);
    }
}
