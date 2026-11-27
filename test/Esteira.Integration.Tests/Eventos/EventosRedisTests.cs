using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Eventos;
using Esteira.Infra.Cache;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Esteira.Integration.Tests.Eventos
{
    // T1.2, critério 13: PUBSUB com Redis:KeyPrefix entre duas instâncias da API (cada uma com a própria conexão, o próprio
    // distribuidor em memória e UMA assinatura do canal), sob a ACL do projeto. Mensagem malformada é descartada.
    [Trait("Fatia", "T1")]
    public sealed class EventosRedisTests : IAsyncLifetime
    {
        private readonly InfraestruturaFixture _infra;
        private readonly string _prefixo = $"{InfraestruturaFixture.PrefixoRedis}{Guid.NewGuid():N}:";
        private readonly List<IAsyncDisposable> _descartaveis = [];
        private ConnectionMultiplexer _admin = null!;
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public EventosRedisTests(InfraestruturaFixture infra) => _infra = infra;

        public async ValueTask InitializeAsync() => _admin = await ConnectionMultiplexer.ConnectAsync(_infra.RedisAdminConnectionString);

        public async ValueTask DisposeAsync()
        {
            foreach (var d in _descartaveis)
                await d.DisposeAsync();
            await _admin.DisposeAsync();
        }

        private sealed record Instancia(RedisPublicadorDeEventos Publicador, DistribuidorDeEventos Distribuidor);

        private async Task<Instancia> NovaInstanciaAsync()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(_infra.RedisConnectionString);
            _descartaveis.Add(redis);
            var distribuidor = new DistribuidorDeEventos(NullLogger<DistribuidorDeEventos>.Instance);
            var assinatura = new RedisAssinaturaDeEventos(redis, distribuidor, _prefixo, NullLogger<RedisAssinaturaDeEventos>.Instance);
            await assinatura.StartAsync(Ct);
            _descartaveis.Add(new Parar(assinatura));
            return new Instancia(new RedisPublicadorDeEventos(redis, _prefixo, NullLogger<RedisPublicadorDeEventos>.Instance), distribuidor);
        }

        private sealed class Parar(RedisAssinaturaDeEventos assinatura) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync() => await assinatura.StopAsync(CancellationToken.None);
        }

        private static async Task<EventoDeTempoReal?> LerAsync(IAssinanteDeEventos assinante, TimeSpan espera)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            cts.CancelAfter(espera);
            try
            {
                return await assinante.Eventos.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        [Fact, DisplayName("13: evento publicado numa instância chega ao stream aberto na outra, pelo canal com Redis:KeyPrefix")]
        public async Task Evento_atravessa_instancias()
        {
            var a = await NovaInstanciaAsync();
            var b = await NovaInstanciaAsync();
            var app = Guid.NewGuid();
            using var naB = b.Distribuidor.Assinar(new HashSet<Guid> { app });
            var evento = new AcaoAtualizada(app, "T1", Guid.NewGuid(), "confirmada");

            await a.Publicador.PublicarAsync(evento, Ct);

            (await LerAsync(naB, TimeSpan.FromSeconds(5))).Should().Be(evento);
        }

        [Fact, DisplayName("13: cada instância mantém uma única assinatura do canal, não uma por stream")]
        public async Task Uma_assinatura_por_instancia()
        {
            var a = await NovaInstanciaAsync();
            var b = await NovaInstanciaAsync();
            var app = Guid.NewGuid();
            using var s1 = a.Distribuidor.Assinar(new HashSet<Guid> { app });
            using var s2 = a.Distribuidor.Assinar(new HashSet<Guid> { app });
            using var s3 = b.Distribuidor.Assinar(new HashSet<Guid> { app });

            var resposta = (RedisResult[])(await _admin.GetDatabase().ExecuteAsync("PUBSUB", "NUMSUB", $"{_prefixo}eventos"))!;

            ((int)resposta[1]).Should().Be(2);
        }

        [Fact, DisplayName("13: o canal leva o Redis:KeyPrefix (nada fora do prefixo do projeto)")]
        public async Task Canal_com_prefixo()
        {
            var a = await NovaInstanciaAsync();
            var canais = (RedisResult[])(await _admin.GetDatabase().ExecuteAsync("PUBSUB", "CHANNELS", $"{_prefixo}*"))!;

            canais.Select(c => (string?)c).Should().Equal($"{_prefixo}eventos");
            a.Should().NotBeNull();
        }

        [Fact, DisplayName("14: mensagem malformada ou de tipo desconhecido é descartada e a assinatura segue viva")]
        public async Task Malformada_e_descartada()
        {
            var a = await NovaInstanciaAsync();
            var app = Guid.NewGuid();
            using var assinante = a.Distribuidor.Assinar(new HashSet<Guid> { app });
            var canal = RedisChannel.Literal($"{_prefixo}eventos");

            await _admin.GetSubscriber().PublishAsync(canal, "isto nao e json");
            await _admin.GetSubscriber().PublishAsync(canal, "{\"tipo\":\"x.y\",\"aplicacaoId\":\"" + app + "\"}");
            await _admin.GetSubscriber().PublishAsync(canal, "{\"tipo\":\"acao.atualizada\",\"aplicacaoId\":\"" + app + "\"}");

            (await LerAsync(assinante, TimeSpan.FromMilliseconds(700))).Should().BeNull();

            var valido = new FatiasMudaram(app, null);
            await a.Publicador.PublicarAsync(valido, Ct);
            (await LerAsync(assinante, TimeSpan.FromSeconds(5))).Should().Be(valido);
        }

        [Fact, DisplayName("14: evento de Aplicação desconhecida (que ninguém vê) não chega a nenhum stream")]
        public async Task Aplicacao_desconhecida_e_descartada()
        {
            var a = await NovaInstanciaAsync();
            var minha = Guid.NewGuid();
            using var assinante = a.Distribuidor.Assinar(new HashSet<Guid> { minha });

            await a.Publicador.PublicarAsync(new FatiasMudaram(Guid.NewGuid(), "T1"), Ct);

            (await LerAsync(assinante, TimeSpan.FromMilliseconds(700))).Should().BeNull();
        }

        [Fact, DisplayName("13: Redis fora do ar não derruba quem publica (loga e segue)")]
        public async Task Publicar_sem_redis_nao_lanca()
        {
            var redis = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:1,abortConnect=false,connectTimeout=200,syncTimeout=200,asyncTimeout=200");
            _descartaveis.Add(redis);
            var publicador = new RedisPublicadorDeEventos(redis, _prefixo, NullLogger<RedisPublicadorDeEventos>.Instance);

            var publicar = () => publicador.PublicarAsync(new FatiasMudaram(Guid.NewGuid(), null), Ct);

            await publicar.Should().NotThrowAsync();
        }
    }
}
