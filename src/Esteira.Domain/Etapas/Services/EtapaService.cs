using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Interfaces;
namespace Esteira.Domain.Etapas.Services
{
    public class EtapaService : IEtapaService
    {
        public void ValidarStatusEtapa(EtapaEntity etapa, StatusEnum status)
        {
            if (etapa == null)
                throw new NotFoundException("Etapa não encontrada");

            if (etapa.Status == status)
                throw new DomainException($"Etapa já está {status.GetDescription().ToUpper()}");
        }

        public void ValidarEtapasExistentesEAtivas(IReadOnlyList<EtapaEntity> etapasEntity, List<string> etapas)
        {
            Guard.AgainstNull(etapasEntity, nameof(etapasEntity));
            Guard.AgainstNull(etapas, nameof(etapas));

            var etapasDict = etapasEntity
                    .ToDictionary(
                        e => e.Sigla.ToUpper(),
                        e => new { e.Descricao, e.Status }
                    );

            var etapasInvalidas = etapas
                .Where(etapa =>
                    !etapasDict.ContainsKey(etapa.ToUpper()) ||
                    etapasDict[etapa.ToUpper()].Status != StatusEnum.ATIVO)
                .ToList();

            if (etapasInvalidas.Any())
            {
                var descricoes = etapasInvalidas
                    .Select(sigla =>
                        etapasDict.TryGetValue(sigla.ToUpper(), out var etapa)
                            ? etapa.Descricao
                            : sigla
                    );

                throw new DomainException(
                    $"Etapas inválidas ou inativas: {string.Join(", ", descricoes)}");
            }
        }

        public void ValidarEtapasObrigatorias(IReadOnlyList<EtapaEntity> etapasEntity, List<string> etapas)
        {
            Guard.AgainstNull(etapasEntity, nameof(etapasEntity));
            Guard.AgainstNull(etapas, nameof(etapas));

            var faltantes = etapasEntity
                           .Where(e => e.Obrigatorio && !etapas.Contains(e.Sigla, StringComparer.OrdinalIgnoreCase))
                           .Select(e => e.Descricao)
                           .ToList();

            if (faltantes.Any())
            {
                throw new DomainException(
                    $"As etapas obrigatórias não foram informadas: {string.Join(", ", faltantes)}");
            }
        }
    }
}
