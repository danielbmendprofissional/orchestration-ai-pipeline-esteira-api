using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy
{
    public class CriarGmudFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        public CriarGmudFluxoDeployCommand(int idFluxoDeploy, string usuario, string justificativa, int? idGmud)
        {
            IdFluxoDeploy = idFluxoDeploy;
            Usuario = usuario;
            Justificativa = justificativa;
            IdGmud = idGmud;
        }
        public int IdFluxoDeploy { get; set; }
        public int? IdGmud { get; set; }
        public string Justificativa { get; set; }
        public string Usuario { get; set; }
    }
}
