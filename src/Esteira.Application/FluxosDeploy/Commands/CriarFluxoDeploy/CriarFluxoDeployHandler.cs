using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy
{
    public class CriarFluxoDeployHandler
        : IRequestHandler<CriarFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly IConfiguracaoSistemaRepository _configuracaoSistemaRepository;
        private readonly IFluxoDeployService _service;
        private readonly IEtapaRepository _etapaRepository;
        private readonly IEtapaService _etapaService;

        public CriarFluxoDeployHandler(
            IFluxoDeployRepository repo,
            IFluxoDeployService service,
            IEtapaRepository etapaRepository,
            IEtapaService etapaService,
            IConfiguracaoSistemaRepository configuracaoSistemaRepository)
        {
            _repo = repo;
            _service = service;
            _etapaRepository = etapaRepository;
            _etapaService = etapaService;
            _configuracaoSistemaRepository = configuracaoSistemaRepository;
        }

        public async Task<BaseMessageResponseDto> Handle(CriarFluxoDeployCommand request, CancellationToken cancellationToken)
        {
            var sistema = StringValueObject.Normalize(request.Sistema);

            if (!await _configuracaoSistemaRepository.ExisteAsync(sistema, cancellationToken))
                await _configuracaoSistemaRepository.AddAsync(ConfiguracaoSistemaEntity.Criar(sistema), cancellationToken);

            var fluxos = await _repo.ObterPorSistemaSomenteEtapasAsync(sistema, request.Status, cancellationToken);

            if (fluxos.Count >= 2)
                throw new DomainException($"Já existem 2 fluxos em EXECUÇÃO para a aplicação {sistema}");

            var fluxoAtual = fluxos.FirstOrDefault();
            _service.ValidarNovoFluxo(fluxoAtual);

            var etapasSistema = await _etapaRepository.GetAllAsync(cancellationToken);
            _etapaService.ValidarEtapasExistentesEAtivas(etapasSistema, request.Etapas);
            _etapaService.ValidarEtapasObrigatorias(etapasSistema, request.Etapas);

            var versaoAnterior = fluxoAtual?.Versao ?? await _repo.ObterUltimaVersaoSistema(sistema, cancellationToken);

            var fluxo = FluxoDeployEntity.Criar(sistema, request.Usuario, versaoAnterior, request.Status);

            var ordem = 1;
            foreach (var etapa in etapasSistema
                         .Where(e => request.Etapas.Contains(e.Sigla, StringComparer.OrdinalIgnoreCase))
                         .OrderBy(e => e.Ordem))
            {
                fluxo.AdicionarEtapa(etapa.Sigla, ordem++);
            }

            _service.ValidarEntradaEtapa(fluxo, fluxo.EtapaAtual()?.SglEtapa, fluxoAtual);

            await _repo.AddAsync(fluxo, cancellationToken);

            return new BaseMessageResponseDto("Fluxo deploy criado com sucesso");
        }
    }
}
