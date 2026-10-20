using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Exceptions;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterCrsBdFluxoDeploy
{
    public class ObterCrsBdFluxoDeployHandler : IRequestHandler<ObterCrsBdFluxoDeployQuery, BaseListaResponseDto<BdCrsDto>>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterCrsBdFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<BaseListaResponseDto<BdCrsDto>> Handle(
            ObterCrsBdFluxoDeployQuery request,
            CancellationToken cancellationToken)
        {
            if (!await _queryRepository.ExisteAsync(request.IdFluxoDeploy, cancellationToken))
                throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            return new BaseListaResponseDto<BdCrsDto>
            {
                Results = await _queryRepository.ListarCrsAsync(request.IdFluxoDeploy, request.Pagination, cancellationToken),
                Pagination = request.Pagination
            };
        }
    }
}
