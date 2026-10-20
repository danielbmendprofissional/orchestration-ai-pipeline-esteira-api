using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Common.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Commands.AdicionarArtefatosAnalise
{
    public class AdicionarArtefatosAnaliseCommand: IRequest<Unit>, ICommand
    {
        public string Ferramenta { get; set; }
        public string TipoAnalise { get; set; }
        public int IdPipeline { get; set; }
        public string Tag { get; set; }
        public int DeployId { get; set; }
        public string ScanId { get; set; }
        public List<ArtefatoCompletoDto> Artefato { get; set; }
        public StatusEnum Status { get; set; }
    }
}
