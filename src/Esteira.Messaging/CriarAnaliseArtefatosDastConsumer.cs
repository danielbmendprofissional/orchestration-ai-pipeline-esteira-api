using Esteira.Application.Deploys.Commands.IniciarAnaliseDinamica;
using Esteira.Application.Deploys.Commands.RegistrarScanAnalise;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.Interfaces.Integracoes;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Esteira.Messaging
{
    public class CriarAnaliseArtefatosDastConsumer : IConsumer<CriarAnaliseArtefatosDastMessage>
    {
        private readonly IMediator _mediator;
        private readonly IScannerDast _scannerDast;
        private readonly ILogger<CriarAnaliseArtefatosDastConsumer> _logger;

        public CriarAnaliseArtefatosDastConsumer(IMediator mediator, IScannerDast scannerDast, ILogger<CriarAnaliseArtefatosDastConsumer> logger)
        {
            _mediator = mediator;
            _scannerDast = scannerDast;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CriarAnaliseArtefatosDastMessage> context)
        {
            var msg = context.Message;

            var idAnalise = await _mediator.Send(new IniciarAnaliseDinamicaCommand
            {
                DeployId = msg.DeployId,
                Ferramenta = msg.Ferramenta,
                IdPipeline = msg.IdPipeline,
                Tag = msg.Tag,
                Url = msg.Url
            });

            if (idAnalise is null)
            {
                _logger.LogWarning(
                    "Análise {Ferramenta} ignorada para o deploy {DeployId}: já existe análise em execução na URL {Url}",
                    msg.Ferramenta, msg.DeployId, msg.Url);
                return;
            }

            string scanId = null;

            try
            {
                scanId = await _scannerDast.ExecutarAnalise(msg.Url, msg.EmailsNotificacao);
            }
            finally
            {
                await _mediator.Send(new RegistrarScanAnaliseCommand { IdAnalise = idAnalise.Value, ScanId = scanId });
            }
        }
    }
}
