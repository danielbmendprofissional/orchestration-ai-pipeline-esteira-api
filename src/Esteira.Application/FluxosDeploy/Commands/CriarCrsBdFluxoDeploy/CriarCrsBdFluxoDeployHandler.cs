using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy
{
    public class CriarCrsBdFluxoDeployHandler : IRequestHandler<CriarCrsBdFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public CriarCrsBdFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarCrsBdFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            foreach (var crs in request.CrsBd)
                fluxoDeploy.AdicionarBd(crs.Crs, crs.Url);

            return new BaseMessageResponseDto("CRS's vinculados ao fluxo de deploy com sucesso!");
        }
    }
}
