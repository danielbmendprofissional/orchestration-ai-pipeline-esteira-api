using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.Interfaces.Transaction;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy
{
    public class CriarAprovacaoNegocioFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        
        public int IdFluxoDeploy { get; set; }
        public List<AprovacaoNegocioDtoRequest> AprovacoesNegocio {  get; set; }

    }
}
