using Esteira.Application.Deploys.Commands.AtualizarAnaliseDast;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.Interfaces.Integracoes;
using MassTransit;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static MassTransit.Monitoring.Performance.BuiltInCounters;

namespace Esteira.Messaging
{
    public class AtualizarAnaliseDastConsumer : IConsumer<AtualizarAnaliseDastMessage>
    {
        private readonly IMediator _mediator;
        private readonly IScannerDast _scannerDast;

        public AtualizarAnaliseDastConsumer(IMediator mediator, IScannerDast scannerDast)
        {
            _mediator = mediator;
            _scannerDast = scannerDast;

        }
        public async Task Consume(ConsumeContext<AtualizarAnaliseDastMessage> context)
        {
            var msg = context.Message;  
            var statusScan = string.Empty;
            var artefatos = await _scannerDast.ObterArtefatosAnalise(msg.ScanId, m => statusScan = m);
            await _mediator.Send(new AtualizarAnaliseDastCommand { Artefatos = artefatos?.ToList() ?? new(), IdAnalise = msg.IdAnalise, StatusScan = statusScan });
        }
    }
}
