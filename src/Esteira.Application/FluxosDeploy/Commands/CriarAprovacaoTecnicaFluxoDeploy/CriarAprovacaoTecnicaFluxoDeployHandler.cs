using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy
{
    public class CriarAprovacaoTecnicaFluxoDeployHandler : IRequestHandler<CriarAprovacaoTecnicaFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly IAprovadorTecnicoRepository _aprovadorTecnicoRepository;

        public CriarAprovacaoTecnicaFluxoDeployHandler(IFluxoDeployRepository repo, IAprovadorTecnicoRepository aprovadorTecnicoRepository)
        {
            _repo = repo;
            _aprovadorTecnicoRepository = aprovadorTecnicoRepository;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarAprovacaoTecnicaFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.IdFluxoDeploy, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.IdFluxoDeploy}'");

            var aprovador = await _aprovadorTecnicoRepository.GetByUsuarioAsync(request.Usuario, cancellationToken)
                ?? throw new NotFoundException($"Usuário aprovador não encontrado na equipe: '{request.Usuario}'");

            var integrantes = await _aprovadorTecnicoRepository.ObterPorEquipeAsync(aprovador.Equipe, cancellationToken);

            fluxoDeploy.AdicionarAprovadorTecnico(aprovador, integrantes, request.Aprovado == 'S', request.Justificativa);

            return new BaseMessageResponseDto($"Aprovação da equipe {aprovador.Equipe} realizada com sucesso!");
        }
    }
}
