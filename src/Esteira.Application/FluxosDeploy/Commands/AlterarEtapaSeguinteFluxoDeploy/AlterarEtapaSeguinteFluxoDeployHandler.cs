using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy
{
    public class AlterarEtapaSeguinteFluxoDeployHandler : IRequestHandler<AlterarEtapaSeguinteFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly IEtapaRepository _etapaRepository;
        private readonly IEnumerable<IEtapaRules> _rules;
        private readonly IFluxoDeployService _service;

        public AlterarEtapaSeguinteFluxoDeployHandler(
            IFluxoDeployRepository repo,
            IEtapaRepository etapaRepository,
            IEnumerable<IEtapaRules> rules,
            IFluxoDeployService service)
        {
            _repo = repo;
            _etapaRepository = etapaRepository;
            _rules = rules;
            _service = service;
        }

        public async Task<BaseMessageResponseDto> Handle(
            AlterarEtapaSeguinteFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var fluxoDeploy = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.Id}'");

            if (fluxoDeploy.Status != StatusEnum.EXECUTANDO)
                throw new DomainException($"Fluxo deploy '{request.Id}' não está em execução");

            var etapaAtual = fluxoDeploy.EtapaAtual()
                ?? throw new DomainException($"Não há etapa em EXECUÇÃO para o fluxo de deploy {request.Id}");

            var regra = _rules.FirstOrDefault(x => x.Etapa.GetDescription().Equals(etapaAtual.SglEtapa, StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException($"Não existem regras configuradas para a etapa {etapaAtual.SglEtapa}");

            var fluxos = await _repo.ObterPorSistemaSomenteEtapasAsync(fluxoDeploy.Sistema, StatusEnum.EXECUTANDO, cancellationToken);
            var outroFluxo = fluxos.FirstOrDefault(a => a.Id != fluxoDeploy.Id);

            _service.ValidarEntradaEtapa(fluxoDeploy, fluxoDeploy.ProximaEtapaNaoIniciada()?.SglEtapa, outroFluxo);

            await regra.RegrasEtapaAsync(fluxoDeploy, cancellationToken);

            fluxoDeploy.AvancarEtapa();

            if (regra.Etapa == EtapaEnum.PRD)
                fluxoDeploy.Concluir();

            var etapa = (await _etapaRepository.ObterCatalogoAsync(cancellationToken))
                .FirstOrDefault(e => e.Sigla.Equals(etapaAtual.SglEtapa, StringComparison.OrdinalIgnoreCase));

            return new BaseMessageResponseDto($"Etapa {etapa?.Descricao ?? etapaAtual.SglEtapa} concluída com sucesso!");
        }
    }
}
