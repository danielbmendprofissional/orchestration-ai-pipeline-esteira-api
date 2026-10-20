using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy
{
    public class CriarVersaoTarefasFluxoDeployHandler
        : IRequestHandler<CriarVersaoTarefasFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public CriarVersaoTarefasFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarVersaoTarefasFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            fluxoDeploy.CriarVersaoTarefas(request.IdProjeto, request.IdVersao);

            return new BaseMessageResponseDto("Versão das tarefas das tarefas vinculados com sucesso no fluxo de deploy!");
        }
    }
}
