using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.FluxosDeploy.Interfaces;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaEspecificaFluxoDeploy
{
    public class AlterarEtapaEspecificaFluxoDeployHandler : IRequestHandler<AlterarEtapaEspecificaFluxoDeployCommand, BaseMessageResponseDto>
    {
        private readonly IFluxoDeployRepository _repo;
        private readonly IFluxoDeployService _service;

        public AlterarEtapaEspecificaFluxoDeployHandler(
            IFluxoDeployRepository repo,
            IFluxoDeployService service)
        {
            _repo = repo;
            _service = service;
        }

        public async Task<BaseMessageResponseDto> Handle(
            AlterarEtapaEspecificaFluxoDeployCommand request,
            CancellationToken cancellationToken)
        {
            var etapa = StringValueObject.Normalize(request.Etapa);

            var fluxoDeploy = await _repo.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Fluxo deploy não encontrado: '{request.Id}'");

            if (fluxoDeploy.Status != StatusEnum.EXECUTANDO)
                throw new DomainException($"Fluxo deploy '{request.Id}' não está em execução");

            var fluxos = await _repo.ObterPorSistemaSomenteEtapasAsync(fluxoDeploy.Sistema, StatusEnum.EXECUTANDO, cancellationToken);
            var outroFluxo = fluxos.FirstOrDefault(a => a.Id != request.Id);

            _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(fluxoDeploy, etapa, outroFluxo);
            _service.ValidarEntradaEtapa(fluxoDeploy, etapa, outroFluxo);

            fluxoDeploy.AtualizarEtapaEspecifica(etapa);

            return new BaseMessageResponseDto("Etapa em execução alterada com sucesso!");
        }
    }
}
