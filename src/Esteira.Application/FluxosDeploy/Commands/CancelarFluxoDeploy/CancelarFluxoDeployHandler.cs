using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CancelarFluxoDeploy
{
    public class CancelarFluxoDeployHandler
        : IRequestHandler<CancelarFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public CancelarFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CancelarFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.Id}'");

            fluxoDeploy.Cancelar();

            return new BaseMessageResponseDto("Fluxo deploy cancelado com sucesso!");
        }
    }
}
