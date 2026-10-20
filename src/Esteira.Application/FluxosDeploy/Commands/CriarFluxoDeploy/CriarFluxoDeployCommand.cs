using Esteira.Application.DTOs.Response.Common;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Common.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy
{
    public class CriarFluxoDeployCommand: IRequest<BaseMessageResponseDto>, ICommand
    {
        public CriarFluxoDeployCommand(string sistema, string usuario, List<string> etapas, StatusEnum status = StatusEnum.EXECUTANDO) 
        { 
            Sistema = sistema;
            Status = status;
            Usuario = usuario;  
            Etapas = etapas;           

        }
        public string Sistema { get; set; }
        public string Usuario { get; set; }         
        public List<string> Etapas { get; set; }
        public StatusEnum Status { get; set; }        
    }
}
