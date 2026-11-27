using Esteira.Application.Eventos;
using MassTransit;

namespace Esteira.Messaging.Consumers
{
    // Consumer fino do registro do hook (T1.4): o RegistradorDeWebhook decide e grava `registrado` ou `falhou` (com código).
    // Falha do GitLab não lança: fica no estado do webhook e o MOR reexecuta. Mensagem repetida é no-op.
    public sealed class RegistrarWebhookPedidoConsumer(RegistradorDeWebhook registrador) : IConsumer<RegistrarWebhookPedido>
    {
        public Task Consume(ConsumeContext<RegistrarWebhookPedido> context)
            => registrador.RegistrarAsync(context.Message.ProjetoId, context.CancellationToken);
    }
}
