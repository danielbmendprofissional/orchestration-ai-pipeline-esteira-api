using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.Settings;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy
{
    public class CriarGmudFluxoDeployHandler : IRequestHandler<CriarGmudFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly GerenciadorTarefasSettings _settings;

        public CriarGmudFluxoDeployHandler(IFluxoDeployRepository repo, IOptions<GerenciadorTarefasSettings> settings)
        {
            _repo = repo;
            _settings = settings.Value;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarGmudFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            fluxoDeploy.CriarGmud(_settings.MontarUrlTarefa(request.IdGmud), request.Justificativa, request.Usuario);

            return new BaseMessageResponseDto(request.IdGmud is null or 0
                ? "Justificativa cadastrada com sucesso para o fluxo de deploy!"
                : $"GMUD #{request.IdGmud} vinculada ao fluxo de deploy com sucesso!");
        }
    }
}
