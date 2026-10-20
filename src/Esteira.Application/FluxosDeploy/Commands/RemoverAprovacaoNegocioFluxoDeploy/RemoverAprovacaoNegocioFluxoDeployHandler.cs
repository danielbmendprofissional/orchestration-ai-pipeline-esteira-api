using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.RemoverAprovacaoNegocioFluxoDeploy
{
    public class RemoverAprovacaoNegocioFluxoDeployHandler : IRequestHandler<RemoverAprovacaoNegocioFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public RemoverAprovacaoNegocioFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            RemoverAprovacaoNegocioFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            if (fluxoDeploy.AprovadoresNegocio.All(a => a.IdTarefa != request.IdTarefa))
                throw new NotFoundException($"Tarefa não vinculada ao fluxo de deploy: '{request.IdFluxoDeploy}'");

            fluxoDeploy.RemoverAprovadorNegocio(request.IdTarefa);

            return new BaseMessageResponseDto($"Tarefa {request.IdTarefa} desvinculada do fluxo de deploy com sucesso!");
        }
    }
}
