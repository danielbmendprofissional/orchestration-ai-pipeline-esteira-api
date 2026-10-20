using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.Interfaces.Transaction;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CancelarFluxoDeploy
{
    public class CancelarFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        int IAlteraFluxoDeploy.IdFluxoDeploy => Id;

        public int Id { get; set; }
    }
}
