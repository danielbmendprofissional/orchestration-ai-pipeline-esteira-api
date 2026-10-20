using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy
{
    public class CriarAprovacaoTecnicaFluxoDeployValidator : AbstractValidator<CriarAprovacaoTecnicaFluxoDeployCommand>
    {
        public CriarAprovacaoTecnicaFluxoDeployValidator()
        {
            RuleFor(x => x.IdFluxoDeploy).NotEmpty().GreaterThan(0);
            RuleFor(x => x.Usuario).NotEmpty()
                .WithMessage("Usuário não identificado");
            RuleFor(x => x.Aprovado)
                .NotEmpty()
                .Must(x => x == 'S' || x == 'N')
                .WithMessage("O campo Obrigatorio deve conter apenas 'S' ou 'N'.");
            RuleFor(x => x.Justificativa)
                .NotEmpty()
                .WithMessage("Justificativa é obrigatória quando a aprovação for negada.")
                .When(x => x.Aprovado == 'N');


        }
    }
}
