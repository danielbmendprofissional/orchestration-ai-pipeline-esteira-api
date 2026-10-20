using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaAmbientePrdService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            Guard.AgainstNull(valueObject.Analises, nameof(valueObject.Analises));
            Guard.AgainstNull(valueObject.Config, nameof(valueObject.Config));

            ValidarDeployAmbiente(valueObject.Analises, "produção");
            ValidarVulnerabilidadeAmbiente(valueObject.Analises, valueObject.Config);
            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.PRD.GetDescription());

            return true;
        }
    }
}
