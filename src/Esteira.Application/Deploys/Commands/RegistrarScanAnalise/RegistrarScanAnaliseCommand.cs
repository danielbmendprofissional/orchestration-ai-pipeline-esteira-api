using Esteira.Application.Interfaces.Transaction;
using MediatR;

namespace Esteira.Application.Deploys.Commands.RegistrarScanAnalise
{
    public class RegistrarScanAnaliseCommand : IRequest<Unit>, ICommand
    {
        public int IdAnalise { get; set; }
        public string ScanId { get; set; }
    }
}
