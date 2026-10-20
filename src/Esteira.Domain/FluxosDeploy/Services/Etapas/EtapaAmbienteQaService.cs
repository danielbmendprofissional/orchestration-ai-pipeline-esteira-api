using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaAmbienteQaService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            Guard.AgainstNull(valueObject.Analises, nameof(valueObject.Analises));

            ValidarDeployAmbiente(valueObject.Analises, "QA");
            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.QA.GetDescription());

            return true;
        }
    }
}