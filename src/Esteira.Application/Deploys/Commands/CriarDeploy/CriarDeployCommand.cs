using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Commands.CriarDeploy
{
    public class CriarDeployCommand : IRequest<Unit>, ICommand
    {   
        public string Sistema { get; set; }
        public int IdPipeline { get; set; }
        public int IdProjetoGit { get; set; }
        public string NomeProjetoGit { get; set; }
        public string Linguagem { get; set; }
        public string Branch { get; set; }
        public string Tag { get; set; }
        public string FerramentaArtefatos { get; set; }
        public string Url { get; set; }
        public ArtefatoAbstractDtoRequest ArtefatoDto { get; set; }
    }

}
