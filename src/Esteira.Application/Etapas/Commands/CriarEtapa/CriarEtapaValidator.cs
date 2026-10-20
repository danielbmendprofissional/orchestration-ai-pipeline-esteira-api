using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Etapas.Commands.CriarEtapa
{
    public class CriarEtapaValidator : AbstractValidator<CriarEtapaCommand>
    {
        public CriarEtapaValidator()
        {
            RuleFor(x => x.Sigla)
                .NotEmpty()
                .MaximumLength(10)
                .Matches(@"^[A-Z0-9_]+$")
                .WithMessage("Sigla deve conter apenas letras maiúsculas, números ou underscore.");
            RuleFor(x => x.Descricao)
                .NotEmpty()
                .MaximumLength(50);
            RuleFor(x => x.Obrigatorio)
                .NotEmpty()
                .Must(x => x == 'S' || x == 'N')
                .WithMessage("O campo Obrigatorio deve conter apenas 'S' ou 'N'.");

        }
    }
}
