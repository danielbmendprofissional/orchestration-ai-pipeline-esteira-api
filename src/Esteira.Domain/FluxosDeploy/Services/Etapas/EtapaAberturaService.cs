using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas.Base;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Services.Etapas
{
    public class EtapaAberturaService : BaseEtapaService, IEtapaRulesService
    {
        public bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject)
        {
            Guard.AgainstNull(valueObject, nameof(valueObject));

            Guard.AgainstNull(valueObject.FluxoDeploy, nameof(valueObject.FluxoDeploy));

            if (!VersaoFluxoValida(valueObject.FluxoDeploy.Versao))
                throw new DomainException("A versão tem que ter que seguir o padrão v1, v2, v3 ...");

            ValidarEtapa(valueObject.FluxoDeploy, EtapaEnum.ABER.GetDescription());

            return true;
        }

        private static bool VersaoFluxoValida(string versao)
        {
            if (string.IsNullOrWhiteSpace(versao))
                return false;

            try
            {
                return VersaoFluxoValueObject.Criar(versao).ToString() == versao;
            }
            catch (DomainException)
            {
                return false;
            }
        }
    }
}
