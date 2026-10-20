using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaAmbienteDevService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            Guard.AgainstNull(valueObject.Analises, nameof(valueObject.Analises));

            ValidarDeployAmbiente(valueObject.Analises, "desenvolvimento");
            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.DEV.GetDescription());

            return true;
        }
    }
}
