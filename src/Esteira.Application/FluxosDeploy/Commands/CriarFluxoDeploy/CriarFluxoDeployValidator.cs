using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy
{
    public class CriarFluxoDeployValidator : AbstractValidator<CriarFluxoDeployCommand>
    {
        public CriarFluxoDeployValidator()
        {
            RuleFor(x => x.Sistema).NotEmpty();
            RuleFor(x => x.Usuario).NotEmpty();            
            RuleFor(x => x.Etapas)
                .Must(etapas => etapas != null && etapas.Count > 0)
                .WithMessage("A lista de etapas deve conter pelo menos um item.");

        }
    }
}
