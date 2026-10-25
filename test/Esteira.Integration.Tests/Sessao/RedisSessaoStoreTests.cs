using Esteira.Application.Interfaces.Sessao;
using Esteira.Infra.Cache;
using Esteira.Integration.Tests.Infraestrutura;
using AwesomeAssertions;
using StackExchange.Redis;

namespace Esteira.Integration.Tests.Sessao
{
    // Redis real com a ACL do projeto (`+@all -@dangerous -scan` em `esteira:*`, como na infra): o adaptador não pode depender de SCAN/KEYS.
    public sealed class RedisSessaoStoreTests : IAsyncLifetime
    {
        private readonly InfraestruturaFixture _infra;
        // Prefixo único por teste, ainda dentro de `esteira:` (a ACL recusa o resto com NOPERM).
        private readonly string _prefixo = $"{InfraestruturaFixture.PrefixoRedis}{Guid.NewGuid():N}:";
        private ConnectionMultiplexer _redis = null!;
        private ConnectionMultiplexer _admin = null!;
        private RedisSessaoStore _store = null!;

        public RedisSessaoStoreTests(InfraestruturaFixture infra) => _infra = infra;

        public async ValueTask InitializeAsync()
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(_infra.RedisConnectionString);
            _admin = await ConnectionMultiplexer.ConnectAsync(_infra.RedisAdminConnectionString);
            _store = new RedisSessaoStore(_redis, _prefixo);
        }

        public async ValueTask DisposeAsync()
        {
            await _redis.DisposeAsync();
            await _admin.DisposeAsync();
        }

        private static SessaoUsuario Sessao(string id, string sub = "user-1", string access = "access-1") =>
            new(id, sub, access, "refresh-1", "id-1", DateTimeOffset.UtcNow.AddMinutes(5));

        private IEnumerable<string> ChavesDoTeste() =>
            _admin.GetServer(_admin.GetEndPoints()[0]).Keys(pattern: $"{_prefixo}*").Select(k => k.ToString());

        [Fact]
        public async Task Criar_guarda_a_sessao_com_ttl_e_obter_devolve_a_mesma()
        {
            var sessao = Sessao("s1");

            await _store.CriarAsync(sessao, TimeSpan.FromSeconds(60));

            var lida = await _store.ObterAsync("s1");
            lida.Should().BeEquivalentTo(sessao);
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync($"{_prefixo}sessao:s1");
            ttl.Should().NotBeNull();
            ttl!.Value.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public async Task Obter_de_sessao_inexistente_devolve_nulo()
        {
            (await _store.ObterAsync("nao-existe")).Should().BeNull();
        }

        [Fact]
        public async Task Criar_indexa_a_sessao_no_conjunto_do_usuario()
        {
            await _store.CriarAsync(Sessao("s1"), TimeSpan.FromSeconds(60));
            await _store.CriarAsync(Sessao("s2"), TimeSpan.FromSeconds(60));

            var membros = await _admin.GetDatabase().SetMembersAsync($"{_prefixo}usuario:user-1:sessoes");
            membros.Select(m => m.ToString()).Should().BeEquivalentTo("s1", "s2");
        }

        [Fact]
        public async Task Atualizar_troca_os_tokens_mantendo_o_id_e_renova_o_ttl()
        {
            await _store.CriarAsync(Sessao("s1"), TimeSpan.FromSeconds(10));

            await _store.AtualizarAsync(Sessao("s1", access: "access-2"), TimeSpan.FromSeconds(300));

            (await _store.ObterAsync("s1"))!.AccessToken.Should().Be("access-2");
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync($"{_prefixo}sessao:s1");
            ttl!.Value.Should().BeGreaterThan(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Revogar_apaga_a_sessao_e_a_tira_do_conjunto_do_usuario()
        {
            await _store.CriarAsync(Sessao("s1"), TimeSpan.FromSeconds(60));
            await _store.CriarAsync(Sessao("s2"), TimeSpan.FromSeconds(60));

            await _store.RevogarAsync("s1");

            (await _store.ObterAsync("s1")).Should().BeNull();
            (await _store.ObterAsync("s2")).Should().NotBeNull();
            var membros = await _admin.GetDatabase().SetMembersAsync($"{_prefixo}usuario:user-1:sessoes");
            membros.Select(m => m.ToString()).Should().BeEquivalentTo("s2");
        }

        [Fact]
        public async Task Revogar_todas_do_usuario_apaga_as_dele_e_o_indice_sem_tocar_nas_de_outro()
        {
            await _store.CriarAsync(Sessao("s1"), TimeSpan.FromSeconds(60));
            await _store.CriarAsync(Sessao("s2"), TimeSpan.FromSeconds(60));
            await _store.CriarAsync(Sessao("s3", sub: "user-2"), TimeSpan.FromSeconds(60));

            await _store.RevogarTodasDoUsuarioAsync("user-1");

            (await _store.ObterAsync("s1")).Should().BeNull();
            (await _store.ObterAsync("s2")).Should().BeNull();
            (await _store.ObterAsync("s3")).Should().NotBeNull();
            (await _admin.GetDatabase().KeyExistsAsync($"{_prefixo}usuario:user-1:sessoes")).Should().BeFalse();
        }

        [Fact]
        public async Task Contexto_de_login_e_de_uso_unico_e_expira()
        {
            var contexto = new ContextoLogin("verifier", "nonce", "/entregas");
            await _store.GuardarLoginAsync("state-1", contexto, TimeSpan.FromMinutes(5));

            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync($"{_prefixo}login:state-1");
            ttl!.Value.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));

            (await _store.ConsumirLoginAsync("state-1")).Should().BeEquivalentTo(contexto);
            (await _store.ConsumirLoginAsync("state-1")).Should().BeNull();
            (await _store.ConsumirLoginAsync("desconhecido")).Should().BeNull();
        }

        [Fact]
        public async Task Trava_de_refresh_e_exclusiva_ate_ser_liberada_e_tem_ttl_curto()
        {
            (await _store.TentarTravarRefreshAsync("s1")).Should().BeTrue();
            (await _store.TentarTravarRefreshAsync("s1")).Should().BeFalse();
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync($"{_prefixo}refresh:s1");
            ttl!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(10));

            await _store.LiberarRefreshAsync("s1");

            (await _store.TentarTravarRefreshAsync("s1")).Should().BeTrue();
        }

        [Fact]
        public async Task Toda_chave_criada_leva_o_prefixo_e_a_ACL_do_projeto_nao_e_violada()
        {
            await _store.CriarAsync(Sessao("s1"), TimeSpan.FromSeconds(60));
            await _store.GuardarLoginAsync("st", new ContextoLogin("v", "n", "/"), TimeSpan.FromMinutes(1));
            await _store.TentarTravarRefreshAsync("s1");
            await _store.AtualizarAsync(Sessao("s1", access: "x"), TimeSpan.FromSeconds(60));
            await _store.RevogarAsync("s1");
            await _store.RevogarTodasDoUsuarioAsync("user-1");

            // O usuário da API só enxerga `esteira:*` (a ACL devolve NOPERM no resto, e nenhuma chamada acima falhou).
            var todas = _admin.GetServer(_admin.GetEndPoints()[0]).Keys(pattern: "*").Select(k => k.ToString());
            todas.Should().OnlyContain(k => k.StartsWith(InfraestruturaFixture.PrefixoRedis));
            ChavesDoTeste().Should().OnlyContain(k => k.StartsWith(_prefixo));
        }
    }
}
