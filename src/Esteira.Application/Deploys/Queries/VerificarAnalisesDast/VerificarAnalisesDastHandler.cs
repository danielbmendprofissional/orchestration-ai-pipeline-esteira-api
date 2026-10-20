using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.Deploys.Messages;
using MassTransit;
using MediatR;

namespace Esteira.Application.Deploys.Queries.VerificarAnalisesDast
{
    public class VerificarAnalisesDastHandler
        : IRequestHandler<VerificarAnalisesDastQuery>
    {
        private readonly IDeployQueryRepository _queryRepository;
        private readonly IPublishEndpoint _bus;

        public VerificarAnalisesDastHandler(IDeployQueryRepository queryRepository, IPublishEndpoint bus)
        {
            _queryRepository = queryRepository;
            _bus = bus;
        }

        public async Task Handle(
            VerificarAnalisesDastQuery request,
            CancellationToken cancellationToken)
        {
            var analises = await _queryRepository.ListarAnalisesDinamicasEmExecucaoAsync(cancellationToken);

            foreach (var analise in analises)
            {
                await _bus.Publish(new AtualizarAnaliseDastMessage
                {
                    IdAnalise = analise.IdAnalise,
                    ScanId = analise.ScanId,
                }, cancellationToken);
            }
        }
    }
}
