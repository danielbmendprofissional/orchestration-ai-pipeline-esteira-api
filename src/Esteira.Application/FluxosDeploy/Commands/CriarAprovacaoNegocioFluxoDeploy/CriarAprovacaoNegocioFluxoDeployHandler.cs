using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy
{
    public class CriarAprovacaoNegocioFluxoDeployHandler : IRequestHandler<CriarAprovacaoNegocioFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;

        public CriarAprovacaoNegocioFluxoDeployHandler(IFluxoDeployRepository repo)
        {
            _repo = repo;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarAprovacaoNegocioFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            foreach (var aprovacao in request.AprovacoesNegocio)
                fluxoDeploy.AdicionarAprovadorNegocio(aprovacao.Tarefa, aprovacao.Url);

            return new BaseMessageResponseDto("Tarefas para aprovação da área de negócio vinculadas com sucesso!");
        }
    }
}
