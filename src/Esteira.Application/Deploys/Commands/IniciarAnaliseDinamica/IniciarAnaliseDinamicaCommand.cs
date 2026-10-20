using Esteira.Application.Interfaces.Transaction;
using MediatR;

namespace Esteira.Application.Deploys.Commands.IniciarAnaliseDinamica
{
    public class IniciarAnaliseDinamicaCommand : IRequest<int?>, ICommand
    {
        public int DeployId { get; set; }
        public string Ferramenta { get; set; }
        public int IdPipeline { get; set; }
        public string Tag { get; set; }
        public string Url { get; set; }
    }
}
