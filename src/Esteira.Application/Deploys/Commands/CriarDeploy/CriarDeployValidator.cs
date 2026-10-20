using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Commands.CriarDeploy
{
    public class CriarDeployValidator : AbstractValidator<CriarDeployCommand>
    {
        public CriarDeployValidator()
        {
            RuleFor(x => x.Sistema).NotEmpty();
            RuleFor(x => x.IdProjetoGit).NotEmpty();
            RuleFor(x => x.Branch).NotEmpty();
            RuleFor(x => x.Tag).NotEmpty();
            RuleFor(x => x.IdPipeline).GreaterThan(0);
            RuleFor(x => x.FerramentaArtefatos).NotEmpty();

        }
    }

}
