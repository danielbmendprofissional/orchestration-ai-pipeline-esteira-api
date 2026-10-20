using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Commands.AdicionarArtefatosAnalise
{
    public class AdicionarArtefatosAnaliseValidator :AbstractValidator<AdicionarArtefatosAnaliseCommand>
    {
        public AdicionarArtefatosAnaliseValidator()
        {
            RuleFor(x => x.TipoAnalise).NotEmpty();
            RuleFor(x => x.Ferramenta).NotEmpty();
            RuleFor(x => x.DeployId).NotEmpty();
            RuleFor(x => x.Status).NotEmpty();
            RuleFor(x => x.Tag).NotEmpty();
            RuleFor(x => x.IdPipeline).NotEmpty().GreaterThan(0);

        }
    }
}
