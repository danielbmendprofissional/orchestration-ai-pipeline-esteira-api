using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaBancoDeDadosService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.BD.GetDescription());

            return true;
        }
    }
}
