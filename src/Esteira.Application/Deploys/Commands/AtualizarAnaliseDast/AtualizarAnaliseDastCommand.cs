using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Transaction;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Commands.AtualizarAnaliseDast
{
    public class AtualizarAnaliseDastCommand: IRequest<Unit>, ICommand
    {
        public int IdAnalise { get; set; }
        public string StatusScan { get; set; }
        public List<ArtefatoCompletoDto>? Artefatos { get; set; }
    }
}
