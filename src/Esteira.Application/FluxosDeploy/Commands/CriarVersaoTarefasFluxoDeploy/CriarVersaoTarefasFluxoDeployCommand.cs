using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy
{
    public class CriarVersaoTarefasFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        public CriarVersaoTarefasFluxoDeployCommand(int idFluxoDeploy, int idProjeto, int idVersao)
        {
            IdFluxoDeploy = idFluxoDeploy;
            IdProjeto = idProjeto;  
            IdVersao = idVersao;
        }

        public int IdFluxoDeploy { get; set; }
        public int IdProjeto { get; set; }
        public int IdVersao { get; set; }
    }
}
