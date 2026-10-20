using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Interfaces;
using MediatR;

namespace Esteira.Application.Etapas.Commands.CriarEtapa
{
    public class CriarEtapaHandler : IRequestHandler<CriarEtapaCommand, BaseMessageResponseDto>
    {
        private readonly IEtapaRepository _etapaRepository;

        public CriarEtapaHandler(IEtapaRepository etapaRepository)
        {
            _etapaRepository = etapaRepository;
        }

        public async Task<BaseMessageResponseDto> Handle(
            CriarEtapaCommand request,
            CancellationToken cancellationToken)
        {
            if (await _etapaRepository.ExisteSiglaAsync(request.Sigla, cancellationToken))
                throw new DomainException($"Já existe uma etapa com a sigla '{request.Sigla}'");

            var ultimaEtapa = await _etapaRepository.ObterUltimaEtapa(cancellationToken);
            var ordem = (ultimaEtapa?.Ordem ?? 0) + 1;

            var etapa = EtapaEntity.Criar(request.Sigla, request.Descricao, ordem, request.Obrigatorio == 'S');

            await _etapaRepository.AddAsync(etapa, cancellationToken);

            return new BaseMessageResponseDto(etapa.Sigla);
        }
    }
}
