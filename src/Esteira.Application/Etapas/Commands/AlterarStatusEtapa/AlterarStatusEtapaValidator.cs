using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Etapas.Commands.AlterarStatusEtapa
{
    public class AlterarStatusEtapaValidator : AbstractValidator<AlterarStatusEtapaCommand>
    {
        public AlterarStatusEtapaValidator()
        {
            RuleFor(x => x.Sigla).NotEmpty();
            RuleFor(x => x.Status.ToUpper())
                .Must(status => new[] { "ATIVO", "INATIVO" }.Contains(status))
                .WithMessage("Status deve ser ATIVO ou INATIVO");


        }
    }
}
