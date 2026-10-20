using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Queries.ObterResumoFluxoDeploy
{
    public class ObterResumoFluxoDeployHandler : IRequestHandler<ObterResumoFluxoDeployQuery, FluxoDeployResumoDto>
    {
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public ObterResumoFluxoDeployHandler(IFluxoDeployQueryRepository queryRepository)
        {
            _queryRepository = queryRepository;
        }

        public async Task<FluxoDeployResumoDto> Handle(
            ObterResumoFluxoDeployQuery request,
            CancellationToken cancellationToken)
        {
            var status = await _queryRepository.ObterStatusAsync(request.Id, cancellationToken)
                ?? throw new DomainException("Fluxo informado inexistente.");

            if (status != StatusEnum.CONCLUIDO)
                throw new DomainException("Fluxo precisa estar concluído.");

            return await _queryRepository.ObterResumoAsync(request.Id, cancellationToken);
        }
    }
}
