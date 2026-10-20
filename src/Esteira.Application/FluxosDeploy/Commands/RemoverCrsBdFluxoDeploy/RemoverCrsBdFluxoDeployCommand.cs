using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.RemoverCrsBdFluxoDeploy
{
    public class RemoverCrsBdFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        public int IdFluxoDeploy { get; set; }
        public int IdCrs { get; set; }
    }
}
