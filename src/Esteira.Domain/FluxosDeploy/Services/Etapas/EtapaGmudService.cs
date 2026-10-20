using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaGmudService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.GMUD.GetDescription());

            return true;
        }
    }
}