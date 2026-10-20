using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.RemoverCrsBdFluxoDeploy
{
    public class RemoverCrsBdFluxoDeployHandler : IRequestHandler<RemoverCrsBdFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public RemoverCrsBdFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            RemoverCrsBdFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            var crs = fluxoDeploy.Bds.FirstOrDefault(a => a.Id == request.IdCrs)
                ?? throw new NotFoundException($"CRS não vinculado ao fluxo de deploy: '{request.IdFluxoDeploy}'");

            fluxoDeploy.RemoverBd(request.IdCrs);

            return new BaseMessageResponseDto($"{crs.Crs} desvinculado do fluxo de deploy com sucesso!");
        }
    }
}
