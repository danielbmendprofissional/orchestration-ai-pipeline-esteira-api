using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapasFluxoDeploy
{
    public class AlterarEtapasFluxoDeployHandler : IRequestHandler<AlterarEtapasFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly IEtapaRepository _etapaRepository;
        private readonly IFluxoDeployService _service;

        public AlterarEtapasFluxoDeployHandler(IFluxoDeployRepository repo, IEtapaRepository etapaRepository, IFluxoDeployService service)
        {
            _repo = repo;
            _etapaRepository = etapaRepository;
            _service = service;
        }

        public async Task<BaseMessageResponseDto> Handle(
            AlterarEtapasFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxo = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Etapas não encontradas para fluxo de deploy: '{request.Id}'");

            var etapasSistema = await _etapaRepository.GetAllAsync(cancellationToken);

            fluxo.AtualizarFluxoDeployEtapas(etapasSistema, request.Etapas);

            var fluxos = await _repo.ObterPorSistemaSomenteEtapasAsync(fluxo.Sistema, StatusEnum.EXECUTANDO, cancellationToken);
            var outroFluxo = fluxos.FirstOrDefault(a => a.Id != fluxo.Id);

            _service.ValidarEntradaEtapa(fluxo, fluxo.EtapaAtual()?.SglEtapa, outroFluxo);

            return new BaseMessageResponseDto("Etapas reconfiguradas com sucesso!");
        }
    }
}
