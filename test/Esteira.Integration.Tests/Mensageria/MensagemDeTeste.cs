using System.Collections.Concurrent;
using MassTransit;

namespace Esteira.Integration.Tests.Mensageria
{
    // Mensagem e consumer que só existem no teste: a M0 não tem consumer de negócio.
    public sealed record MensagemDeTeste(Guid Marcador);

    public sealed class ConsumidorDeTeste : IConsumer<MensagemDeTeste>
    {
        public static readonly ConcurrentDictionary<Guid, int> Recebidas = new();

        public Task Consume(ConsumeContext<MensagemDeTeste> context)
        {
            Recebidas.AddOrUpdate(context.Message.Marcador, 1, (_, vezes) => vezes + 1);
            return Task.CompletedTask;
        }

        public static int Vezes(Guid marcador) => Recebidas.GetValueOrDefault(marcador);

        public static async Task<bool> AguardarAsync(Guid marcador, TimeSpan limite)
        {
            var fim = DateTime.UtcNow + limite;
            while (DateTime.UtcNow < fim)
            {
                if (Vezes(marcador) > 0)
                    return true;
                await Task.Delay(100);
            }
            return false;
        }
    }
}
