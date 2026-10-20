using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapasFluxoDeploy
{
    public class AlterarEtapasFluxoDeployValidator : AbstractValidator<AlterarEtapasFluxoDeployCommand>
    {
        public AlterarEtapasFluxoDeployValidator()
        {
            RuleFor(x => x.Id).NotEmpty().GreaterThan(0);           
            RuleFor(x => x.Etapas)
                .Must(etapas => etapas != null && etapas.Count > 0)
                .WithMessage("A lista de etapas deve conter pelo menos um item.");

        }
    }
    
}
