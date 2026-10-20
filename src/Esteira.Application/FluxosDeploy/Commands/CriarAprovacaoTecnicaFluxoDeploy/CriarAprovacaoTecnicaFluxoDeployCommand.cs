using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy
{
    public class CriarAprovacaoTecnicaFluxoDeployCommand:  IRequest<BaseMessageResponseDto>, ICommand
    {
        public CriarAprovacaoTecnicaFluxoDeployCommand(int idFluxoDeploy, string usuario, char aprovado, string justificativa)
        {
            IdFluxoDeploy = idFluxoDeploy;
            Usuario = usuario;
            Aprovado = aprovado;
            Justificativa = justificativa;
        }
        public int IdFluxoDeploy { get; set; } 
        public string Usuario { get; set; }
        public char Aprovado { get; set; }
        public string Justificativa { get; set; }

    }
}
