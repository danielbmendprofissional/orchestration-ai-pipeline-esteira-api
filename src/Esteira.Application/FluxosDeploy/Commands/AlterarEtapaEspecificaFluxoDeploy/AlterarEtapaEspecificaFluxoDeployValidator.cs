using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaEspecificaFluxoDeploy
{
    public class AlterarEtapaEspecificaFluxoDeployValidator : AbstractValidator<AlterarEtapaEspecificaFluxoDeployCommand>
    {
        public AlterarEtapaEspecificaFluxoDeployValidator() 
        {
            RuleFor(x => x.Id).NotEmpty().GreaterThan(0);
            RuleFor(x => x.Etapa).NotEmpty();
        }
    }
}
