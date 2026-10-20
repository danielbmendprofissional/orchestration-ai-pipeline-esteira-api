using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy
{
    public class CriarGmudFluxoDeployValidator : AbstractValidator<CriarGmudFluxoDeployCommand>
    {
        public CriarGmudFluxoDeployValidator()
        {
            RuleFor(x => x.IdFluxoDeploy).NotEmpty().GreaterThan(0);
            RuleFor(x => x.Usuario).NotEmpty()
                .WithMessage("Usuário não identificado");
            RuleFor(x => x.Justificativa)
                .NotEmpty()
                .WithMessage("Justificativa é obrigatória quando uma GMUD não for informada.")
                .When(x => x.IdGmud == 0 || x.IdGmud == null);
        }
    }
}
