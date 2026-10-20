using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy
{
    public class CriarCrsBdFluxoDeployValidator : AbstractValidator<CriarCrsBdFluxoDeployCommand>
    {
        public CriarCrsBdFluxoDeployValidator()
        {
            RuleFor(x => x.IdFluxoDeploy).NotEmpty().GreaterThan(0);
            RuleFor(x => x.CrsBd)
                .Must(x => x != null && x.Count > 0)
                .WithMessage("Deve existir na lista pelo menos 1 item!");
            RuleForEach(x => x.CrsBd)
                .Must(x => !string.IsNullOrWhiteSpace(x.Crs))
                .WithMessage("O campo Crs deve ser informado.");

        }
    }
}
