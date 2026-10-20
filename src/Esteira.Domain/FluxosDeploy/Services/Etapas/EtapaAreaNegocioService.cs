using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaAreaNegocioService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            Guard.AgainstNull(valueObject.Tarefas, nameof(valueObject.Tarefas));

            var tarefasNaoAprovadas = valueObject.Tarefas
                .Where(t => !t.EstaAprovada)
                .ToList();

            if (tarefasNaoAprovadas.Any())
                throw new DomainException($"As tarefas não estão aprovadas. IDs: {string.Join(", ", tarefasNaoAprovadas.Select(t => t.Id))}");

            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.AAN.GetDescription());

            return true;
        }
    }
}
