using Esteira.Application.Deploys.Commands.AdicionarArtefatosAnalise;
using Esteira.Application.Deploys.Messages;
using Esteira.Domain.Common.Enums;
using MassTransit;
using MediatR;

namespace Esteira.Messaging
{

    public class CriarAnaliseArtefatosSastConsumer : IConsumer<CriarAnaliseArtefatosSastMessage>
    {
        private readonly IMediator _mediator;
        public CriarAnaliseArtefatosSastConsumer(IMediator mediator)
        {
            _mediator = mediator;
        }
        public async Task Consume(ConsumeContext<CriarAnaliseArtefatosSastMessage> context)
        {
            var msg = context.Message;

            await _mediator.Send(new AdicionarArtefatosAnaliseCommand { 
                DeployId = msg.DeployId,
                Ferramenta = msg.Ferramenta,
                TipoAnalise = "SAST",
                Artefato = msg.Artefato,
                Status = StatusEnum.CONCLUIDO,
                Tag = msg.Tag,
                IdPipeline = msg.IdPipeline,
            });
           
        }
    }

}
