using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using MediatR;

namespace Esteira.Application.Deploys.Queries.ObterArtefatosAnalise
{
    public class ObterArtefatosAnaliseHandler : IRequestHandler<ObterArtefatosAnaliseQuery, BaseListaResponseDto<ArtefatoDto>>
    {
        private readonly IDeployQueryRepository _queryRepository;

        public ObterArtefatosAnaliseHandler(IDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<BaseListaResponseDto<ArtefatoDto>> Handle(
            ObterArtefatosAnaliseQuery request,
            CancellationToken cancellationToken)
            => new()
            {
                Results = await _queryRepository.ListarArtefatosAsync(request.IdDeploy, request.Pagination, cancellationToken),
                Pagination = request.Pagination
            };
    }
}
