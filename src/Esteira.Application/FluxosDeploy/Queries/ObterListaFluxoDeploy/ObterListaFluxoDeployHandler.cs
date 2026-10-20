using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterListaFluxoDeploy
{
    public class ObterListaFluxoDeployHandler : IRequestHandler<ObterListaFluxoDeployQuery, BaseListaResponseDto<FluxoDeployDto>>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterListaFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<BaseListaResponseDto<FluxoDeployDto>> Handle(ObterListaFluxoDeployQuery request, CancellationToken cancellationToken)
        {
            var filtro = new FiltroFluxoDeploy(request.Sistema, request.DataInicio, request.DataFim, request.Status);

            return new BaseListaResponseDto<FluxoDeployDto>
            {
                Results = await _queryRepository.ListarAsync(filtro, request.Pagination, cancellationToken),
                Pagination = request.Pagination
            };
        }
    }
}
