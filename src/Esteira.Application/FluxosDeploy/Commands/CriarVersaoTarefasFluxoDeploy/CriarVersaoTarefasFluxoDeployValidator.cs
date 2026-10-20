using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy
{
    public class CriarVersaoTarefasFluxoDeployValidator : AbstractValidator<CriarVersaoTarefasFluxoDeployCommand>
    {
        public CriarVersaoTarefasFluxoDeployValidator()
        {
            RuleFor(x => x.IdFluxoDeploy).NotEmpty().GreaterThan(0);
            RuleFor(x => x.IdProjeto).NotEmpty().GreaterThan(0);
            RuleFor(x => x.IdVersao).NotEmpty().GreaterThan(0);
        }
    }
}
