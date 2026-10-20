using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.RemoverAprovacaoNegocioFluxoDeploy
{
    public class RemoverAprovacaoNegocioFluxoDeployCommand:IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        public RemoverAprovacaoNegocioFluxoDeployCommand(int idFluxoDeploy, int idTarefa)
        {
            IdFluxoDeploy = idFluxoDeploy;
            IdTarefa = idTarefa;
        }

        public int IdFluxoDeploy { get; set; }
        public int IdTarefa { get; set; }
    }
}
