using Esteira.Domain.FluxosDeploy.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.FluxosDeploy.Interfaces
{
    public interface IEtapaRulesService
    {   
        bool ValidarConclusaoEtapa(ValidacaoEtapaValueObject valueObject);
    }
}
