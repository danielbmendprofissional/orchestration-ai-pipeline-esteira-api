using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Infra.Cache;
using Esteira.Integration.Tests.Infraestrutura;
using NSubstitute;
using StackExchange.Redis;

namespace Esteira.Integration.Tests.Fatias
{
    // P1.5 contra Redis real com a ACL do projeto (sem SCAN/KEYS): SET NX, TTL, trava de leitura única entre instâncias.
    [Trait("Fatia", "P1")]
    public sealed class RedisPainelCacheTests : IAsyncLifetime
    {
        private readonly InfraestruturaFixture _infra;
        private readonly string _prefixo = $"{InfraestruturaFixture.PrefixoRedis}{Guid.NewGuid():N}:";
        private ConnectionMultiplexer _redis = null!;
        private ConnectionMultiplexer _admin = null!;
        private RedisPainelCache _cache = null!;
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public RedisPainelCacheTests(InfraestruturaFixture infra) => _infra = infra;

        public async ValueTask InitializeAsync()
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(_infra.RedisConnectionString);
            _admin = await ConnectionMultiplexer.ConnectAsync(_infra.RedisAdminConnectionString);
            _cache = new RedisPainelCache(_redis, _prefixo);
        }

        public async ValueTask DisposeAsync()
        {
            await _redis.DisposeAsync();
            await _admin.DisposeAsync();
        }

        [Fact]
        public async Task Guarda_com_prefixo_e_ttl_e_le_de_volta()
        {
            await _cache.GuardarAsync("painel:a", "valor", TimeSpan.FromSeconds(60), Ct);

            (await _cache.ObterAsync("painel:a", Ct)).Should().Be("valor");
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync($"{_prefixo}painel:a");
            ttl.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(60)).And.BeGreaterThan(TimeSpan.FromSeconds(50));
            (await _cache.ObterAsync("painel:inexistente", Ct)).Should().BeNull();
        }

        [Fact]
        public async Task Guardar_se_ausente_so_o_primeiro_ganha()
        {
            var ganhou = await Task.WhenAll(Enumerable.Range(0, 10)
                .Select(i => _cache.TentarGuardarSeAusenteAsync("painel:trava", $"dono-{i}", TimeSpan.FromSeconds(30), Ct)));

            ganhou.Count(g => g).Should().Be(1);
        }

        [Fact]
        public async Task Remover_se_valor_so_apaga_o_dono()
        {
            await _cache.GuardarAsync("painel:t", "dono-1", TimeSpan.FromSeconds(30), Ct);

            await _cache.RemoverSeValorAsync("painel:t", "outro", Ct);
            (await _cache.ObterAsync("painel:t", Ct)).Should().Be("dono-1");

            await _cache.RemoverSeValorAsync("painel:t", "dono-1", Ct);
            (await _cache.ObterAsync("painel:t", Ct)).Should().BeNull();
        }

        [Fact]
        public async Task Remover_apaga()
        {
            await _cache.GuardarAsync("painel:r", "x", TimeSpan.FromSeconds(30), Ct);
            await _cache.RemoverAsync("painel:r", Ct);
            (await _cache.ObterAsync("painel:r", Ct)).Should().BeNull();
        }

        [Fact]
        public async Task Duas_instancias_do_servico_fazem_uma_leitura_so_no_gitlab()
        {
            var liberar = new TaskCompletionSource();
            var gitlab = Substitute.For<IGitLab>();
            gitlab.ObterArquivoAsync("valuto", 10, "specs/README.md", "main", Arg.Any<CancellationToken>())
                .Returns(async _ => { await liberar.Task; return Result<string?>.Sucesso("# Specs\n"); });
            gitlab.ObterArquivoAsync("valuto", 10, Arg.Is<string>(c => c != "specs/README.md"), "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(null));
            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso([]));
            gitlab.ListarMergeRequestsAsync("valuto", Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([]));
            var opcoes = new PainelOptions { IntervaloDeEsperaMs = 10 };
            var s1 = new PainelDeFatias(gitlab, _cache, TimeProvider.System, opcoes);
            var s2 = new PainelDeFatias(gitlab, new RedisPainelCache(_redis, _prefixo), TimeProvider.System, opcoes);
            var app = new AplicacaoDoPainel("app", "valuto", 10, new Dictionary<RepositorioFatia, long> { [RepositorioFatia.Api] = 20 });

            var t = Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(() => (i % 2 == 0 ? s1 : s2).ObterAsync(app, Ct))));
            await Task.Delay(300, Ct);
            liberar.SetResult();
            var rs = await t;

            rs.Should().OnlyContain(r => r.Situacao == SituacaoDoPainel.Ok);
            await gitlab.Received(1).ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>());
        }
    }
}
