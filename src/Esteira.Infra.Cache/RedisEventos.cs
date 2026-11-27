using Esteira.Application.Eventos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Esteira.Infra.Cache
{
    // Tempo real no Redis (ADR-0011). Canal único `<Redis:KeyPrefix>eventos`; só PUBLISH/SUBSCRIBE (a ACL do projeto
    // libera canais `<projeto>:*` e nega SCAN/KEYS). O corpo da mensagem nunca vai a log.
    public static class CanalDeEventos
    {
        public static RedisChannel Nome(string prefixo) => RedisChannel.Literal($"{prefixo}eventos");
    }

    public sealed class RedisPublicadorDeEventos(IConnectionMultiplexer redis, string prefixo, ILogger<RedisPublicadorDeEventos> log) : IPublicadorDeEventos
    {
        public async Task PublicarAsync(EventoDeTempoReal evento, CancellationToken cancellationToken = default)
        {
            try
            {
                await redis.GetSubscriber().PublishAsync(CanalDeEventos.Nome(prefixo), CodecDeEventos.Serializar(evento));
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
            {
                // Quem publica já comitou: o tempo real é só aceleração (a consulta periódica e o recarregar do front cobrem).
                log.LogWarning("Evento {Tipo} não publicado no Redis: {Falha}", evento.Tipo, ex.GetType().Name);
            }
        }
    }

    // UMA assinatura do canal por instância; distribui em memória aos streams abertos (fan-out).
    public sealed class RedisAssinaturaDeEventos(
        IConnectionMultiplexer redis, IDistribuidorDeEventos distribuidor, string prefixo, ILogger<RedisAssinaturaDeEventos> log) : IHostedService
    {
        private static readonly TimeSpan EsperaEntreTentativas = TimeSpan.FromSeconds(5);

        private CancellationTokenSource? _parar;
        private Task? _tentativas;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _parar = new CancellationTokenSource();
            if (!await TentarAssinarAsync())
                _tentativas = Task.Run(() => RepetirAsync(_parar.Token), CancellationToken.None);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_parar is not null)
                await _parar.CancelAsync();
            if (_tentativas is not null)
                await _tentativas.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            try
            {
                await redis.GetSubscriber().UnsubscribeAsync(CanalDeEventos.Nome(prefixo));
            }
            catch (Exception ex) when (ex is RedisException or ObjectDisposedException)
            {
                // na parada não há o que fazer: a conexão cai junto
            }
        }

        private async Task RepetirAsync(CancellationToken ct)
        {
            try
            {
                while (!await TentarAssinarAsync())
                    await Task.Delay(EsperaEntreTentativas, ct);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<bool> TentarAssinarAsync()
        {
            try
            {
                await redis.GetSubscriber().SubscribeAsync(CanalDeEventos.Nome(prefixo), AoReceber);
                return true;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                log.LogWarning("Assinatura do canal de eventos indisponível, nova tentativa em instantes: {Falha}", ex.GetType().Name);
                return false;
            }
        }

        private void AoReceber(RedisChannel canal, RedisValue valor)
        {
            if (!CodecDeEventos.TentarLer(valor.IsNull ? null : (string?)valor, out var evento) || evento is null)
            {
                // Sem o corpo: pode ter texto que não deveria estar ali.
                log.LogWarning("Mensagem do canal de eventos descartada (malformada ou de tipo desconhecido)");
                return;
            }

            distribuidor.Distribuir(evento);
        }
    }
}
