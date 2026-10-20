using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy
{
    public class CriarAprovacaoNegocioFluxoDeployValidator : AbstractValidator<CriarAprovacaoNegocioFluxoDeployCommand>
    {
        public CriarAprovacaoNegocioFluxoDeployValidator()
        {
            RuleFor(x => x.IdFluxoDeploy).NotEmpty().GreaterThan(0);
            RuleFor(x => x.AprovacoesNegocio)
                .Must(x => x != null && x.Count > 0)
                .WithMessage("Deve existir na lista pelo menos 1 item!");
            RuleForEach(x => x.AprovacoesNegocio)
                .ChildRules(item =>
                {
                    item.RuleFor(x => x.Tarefa)
                        .GreaterThan(0)
                        .WithMessage("O campo Tarefa deve ser maior que zero.");
                });

        }
    }
}
