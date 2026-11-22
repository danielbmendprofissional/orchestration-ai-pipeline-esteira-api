using System.Collections.Concurrent;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P1.5, critérios 13, 14 e 15: guardado de 60 s, leitura única, Atualizar com limite, GitLab fora, falha isolada.
    [Trait("Fatia", "P1")]
    public class PainelDeFatiasTests
    {
        private const string CaminhoSpecP1 = "specs/feature/painel/01-painel-de-fatias.md";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly string Mapa = FixturesFatias.Ler("mapa-de-fatias.md");
        private static readonly Erro Indisponivel = new("gitlab.indisponivel", "fora", TipoErro.Failure);
        private static readonly Erro SemBot = new("gitlab.bot-nao-configurado", "sem bot", TipoErro.Failure);

        private sealed class Relogio : TimeProvider
        {
            public DateTimeOffset Agora { get; set; } = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Agora;
        }

        private sealed class CacheFalso(Relogio relogio) : IPainelCache
        {
            private readonly Dictionary<string, (string Valor, DateTimeOffset Expira)> _d = new();
            private readonly object _trava = new();

            public Task<string?> ObterAsync(string chave, CancellationToken ct = default)
            {
                lock (_trava)
                    return Task.FromResult(_d.TryGetValue(chave, out var e) && e.Expira > relogio.Agora ? e.Valor : null);
            }

            public Task GuardarAsync(string chave, string valor, TimeSpan validade, CancellationToken ct = default)
            {
                lock (_trava) _d[chave] = (valor, relogio.Agora + validade);
                return Task.CompletedTask;
            }

            public Task<bool> TentarGuardarSeAusenteAsync(string chave, string valor, TimeSpan validade, CancellationToken ct = default)
            {
                lock (_trava)
                {
                    if (_d.TryGetValue(chave, out var e) && e.Expira > relogio.Agora)
                        return Task.FromResult(false);
                    _d[chave] = (valor, relogio.Agora + validade);
                    return Task.FromResult(true);
                }
            }

            public Task RemoverAsync(string chave, CancellationToken ct = default)
            {
                lock (_trava) _d.Remove(chave);
                return Task.CompletedTask;
            }

            public Task RemoverSeValorAsync(string chave, string valor, CancellationToken ct = default)
            {
                lock (_trava)
                    if (_d.TryGetValue(chave, out var e) && e.Valor == valor)
                        _d.Remove(chave);
                return Task.CompletedTask;
            }
        }

        private static AplicacaoDoPainel App(string chave = "app-1", long? workspace = 10) => new(
            chave, "valuto", workspace,
            new Dictionary<RepositorioFatia, long> { [RepositorioFatia.Api] = 20, [RepositorioFatia.Frontend] = 30 });

        private static IGitLab GitLabOk(Func<Task>? aoLerMapa = null)
        {
            var gitlab = Substitute.For<IGitLab>();
            gitlab.ObterArquivoAsync("valuto", 10, "specs/README.md", "main", Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    if (aoLerMapa is not null) await aoLerMapa();
                    return Result<string?>.Sucesso(Mapa);
                });
            gitlab.ObterArquivoAsync("valuto", 10, CaminhoSpecP1, "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(FixturesFatias.Ler("spec-p1-cabecalho.md")));
            gitlab.ObterArquivoAsync("valuto", 10, Arg.Is<string>(c => c != "specs/README.md" && c != CaminhoSpecP1), "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(null));
            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso(
                    [new IssueGitLabDto(37, "P1 — Painel de Fatias", "opened", ["em-execucao"], FixturesFatias.Ler("bloco-execucao-b2.md"))]));
            gitlab.ListarMergeRequestsAsync("valuto", Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([]));
            return gitlab;
        }

        private static (PainelDeFatias Servico, Relogio Relogio) Criar(IGitLab gitlab, Relogio? relogio = null)
        {
            relogio ??= new Relogio();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(relogio), relogio, new PainelOptions { IntervaloDeEsperaMs = 5 });
            return (servico, relogio);
        }

        private static int LeiturasDeArquivo(IGitLab gitlab) =>
            gitlab.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IGitLab.ObterArquivoAsync));

        private static int LeiturasDoMapa(IGitLab gitlab) =>
            gitlab.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IGitLab.ObterArquivoAsync) && (string)c.GetArguments()[2]! == "specs/README.md");

        [Fact, DisplayName("13: monta as Fatias com estado e progresso a partir do mapa, issues e bloco da mãe")]
        public async Task Monta_as_fatias()
        {
            var (servico, _) = Criar(GitLabOk());

            var r = await servico.ObterAsync(App(), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Ok);
            r.Desatualizado.Should().BeFalse();
            r.GeradoEm.Should().NotBeNull();
            var p1 = r.Fatias.Single(f => f.Codigo == "P1");
            p1.Estado.Estado.Should().Be(EstadoDaFatia.EmExecucao);
            p1.Estado.Progresso.Should().Be("2 de 3 tasks");
            p1.Risco.Should().Be("alto");
        }

        [Fact, DisplayName("13: pedido dentro de 60 s responde do guardado sem consultar o GitLab")]
        public async Task Dentro_da_validade_nao_consulta_o_gitlab()
        {
            var gitlab = GitLabOk();
            var (servico, relogio) = Criar(gitlab);
            await servico.ObterAsync(App(), Ct);
            relogio.Agora += TimeSpan.FromSeconds(59);

            var r = await servico.ObterAsync(App(), Ct);

            r.Fatias.Should().NotBeEmpty();
            LeiturasDoMapa(gitlab).Should().Be(1);
            await gitlab.Received(1).ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("13: passados 60 s lê de novo")]
        public async Task Vencido_le_de_novo()
        {
            var gitlab = GitLabOk();
            var (servico, relogio) = Criar(gitlab);
            await servico.ObterAsync(App(), Ct);
            relogio.Agora += TimeSpan.FromSeconds(61);

            await servico.ObterAsync(App(), Ct);

            LeiturasDoMapa(gitlab).Should().Be(2);
        }

        [Fact, DisplayName("13: Painel:ValidadeSegundos muda a validade do guardado")]
        public async Task Validade_configuravel()
        {
            var gitlab = GitLabOk();
            var relogio = new Relogio();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(relogio), relogio, new PainelOptions { ValidadeSegundos = 5, IntervaloDeEsperaMs = 5 });
            await servico.ObterAsync(App(), Ct);
            relogio.Agora += TimeSpan.FromSeconds(6);

            await servico.ObterAsync(App(), Ct);

            LeiturasDoMapa(gitlab).Should().Be(2);
        }

        [Fact, DisplayName("13: chegadas simultâneas com o guardado vencido fazem uma leitura só")]
        public async Task Simultaneas_fazem_uma_leitura()
        {
            var liberar = new TaskCompletionSource();
            var gitlab = GitLabOk(() => liberar.Task);
            var (servico, _) = Criar(gitlab);

            var chegadas = Enumerable.Range(0, 6).Select(_ => Task.Run(() => servico.ObterAsync(App(), Ct))).ToArray();
            await Task.Delay(100, Ct);
            liberar.SetResult();
            var resultados = await Task.WhenAll(chegadas);

            LeiturasDoMapa(gitlab).Should().Be(1);
            await gitlab.Received(1).ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>());
            resultados.Should().OnlyContain(r => r.Situacao == SituacaoDoPainel.Ok && r.Fatias.Count == 34);
        }

        [Fact, DisplayName("13: cabeçalho da spec fica 10 min no guardado e a leitura das specs usa no máximo 8 em paralelo")]
        public async Task Cabecalho_da_spec_em_cache_e_paralelismo_limitado()
        {
            var gitlab = GitLabOk();
            var atuais = 0;
            var maximo = 0;
            gitlab.ObterArquivoAsync("valuto", 10, Arg.Is<string>(c => c != "specs/README.md"), "main", Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    var n = Interlocked.Increment(ref atuais);
                    InterlockedMax(ref maximo, n);
                    await Task.Delay(30);
                    Interlocked.Decrement(ref atuais);
                    return Result<string?>.Sucesso(FixturesFatias.Ler("spec-p1-cabecalho.md"));
                });
            var (servico, relogio) = Criar(gitlab);
            await servico.ObterAsync(App(), Ct);
            var leiturasDeSpec = LeiturasDeArquivo(gitlab) - 1;

            relogio.Agora += TimeSpan.FromSeconds(61);
            await servico.ObterAsync(App(), Ct);
            LeiturasDeArquivo(gitlab).Should().Be(2 + leiturasDeSpec);

            relogio.Agora += TimeSpan.FromMinutes(11);
            await servico.ObterAsync(App(), Ct);
            LeiturasDeArquivo(gitlab).Should().Be(3 + 2 * leiturasDeSpec);
            maximo.Should().BeInRange(2, 8);
        }

        private static void InterlockedMax(ref int alvo, int valor)
        {
            int atual;
            while (valor > (atual = Volatile.Read(ref alvo)))
                if (Interlocked.CompareExchange(ref alvo, valor, atual) == atual) break;
        }

        [Fact, DisplayName("14: Atualizar descarta o guardado e a próxima leitura vai ao GitLab")]
        public async Task Atualizar_descarta()
        {
            var gitlab = GitLabOk();
            var (servico, _) = Criar(gitlab);
            await servico.ObterAsync(App(), Ct);

            var atualizar = await servico.AtualizarAsync(App(), Ct);
            await servico.ObterAsync(App(), Ct);

            atualizar.IsSuccess.Should().BeTrue();
            LeiturasDoMapa(gitlab).Should().Be(2);
        }

        [Fact, DisplayName("14: segunda atualização da mesma Aplicação em menos de 10 s é recusada; outra Aplicação não")]
        public async Task Atualizar_tem_limite_de_10_segundos()
        {
            var (servico, relogio) = Criar(GitLabOk());
            await servico.AtualizarAsync(App(), Ct);
            relogio.Agora += TimeSpan.FromSeconds(9);

            var segunda = await servico.AtualizarAsync(App(), Ct);
            var outra = await servico.AtualizarAsync(App("app-2"), Ct);
            relogio.Agora += TimeSpan.FromSeconds(2);
            var depois = await servico.AtualizarAsync(App(), Ct);

            segunda.IsSuccess.Should().BeFalse();
            segunda.Errors[0].Code.Should().Be("painel.atualizacao-recente");
            segunda.Errors[0].Type.Should().Be(TipoErro.TooManyRequests);
            outra.IsSuccess.Should().BeTrue();
            depois.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("15: GitLab fora com cópia: devolve a cópia desatualizada com o geradoEm de quando foi lida")]
        public async Task Gitlab_fora_serve_a_copia()
        {
            var gitlab = GitLabOk();
            var (servico, relogio) = Criar(gitlab);
            var primeiro = await servico.ObterAsync(App(), Ct);
            gitlab.ObterArquivoAsync("valuto", 10, "specs/README.md", "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Falha(Indisponivel));
            relogio.Agora += TimeSpan.FromMinutes(30);

            var r = await servico.ObterAsync(App(), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Ok);
            r.Desatualizado.Should().BeTrue();
            r.GeradoEm.Should().Be(primeiro.GeradoEm);
            r.Fatias.Should().HaveCount(34);
        }

        [Fact, DisplayName("15: a cópia dura 24 h; depois disso, sem guardado, o erro gitlab.indisponivel")]
        public async Task Copia_vence_em_24_horas()
        {
            var gitlab = GitLabOk();
            var (servico, relogio) = Criar(gitlab);
            await servico.ObterAsync(App(), Ct);
            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<IssueGitLabDto>>.Falha(Indisponivel));

            relogio.Agora += TimeSpan.FromHours(23);
            (await servico.ObterAsync(App(), Ct)).Desatualizado.Should().BeTrue();

            relogio.Agora += TimeSpan.FromHours(2);
            var r = await servico.ObterAsync(App(), Ct);
            r.Situacao.Should().Be(SituacaoDoPainel.Falha);
            r.Erro!.Code.Should().Be("gitlab.indisponivel");
        }

        [Fact, DisplayName("15: GitLab fora sem cópia: falha da Aplicação com gitlab.indisponivel")]
        public async Task Gitlab_fora_sem_copia()
        {
            var gitlab = GitLabOk();
            gitlab.ListarMergeRequestsAsync("valuto", 20, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Falha(Indisponivel));
            var (servico, _) = Criar(gitlab);

            var r = await servico.ObterAsync(App(), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Falha);
            r.Erro!.Code.Should().Be("gitlab.indisponivel");
            r.Fatias.Should().BeEmpty();
        }

        [Fact, DisplayName("15: sem token para o grupo: gitlab.bot-nao-configurado")]
        public async Task Sem_bot()
        {
            var gitlab = Substitute.For<IGitLab>();
            gitlab.ObterArquivoAsync(default!, default, default!, default!, default).ReturnsForAnyArgs(Result<string?>.Falha(SemBot));
            gitlab.ListarIssuesAsync(default!, default, default).ReturnsForAnyArgs(Result<IReadOnlyList<IssueGitLabDto>>.Falha(SemBot));
            gitlab.ListarMergeRequestsAsync(default!, default, default).ReturnsForAnyArgs(Result<IReadOnlyList<MergeRequestGitLabDto>>.Falha(SemBot));
            var (servico, _) = Criar(gitlab);

            var r = await servico.ObterAsync(App(), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Falha);
            r.Erro!.Code.Should().Be("gitlab.bot-nao-configurado");
        }

        [Fact, DisplayName("15: Aplicação sem workspace: aviso, sem chamar o GitLab")]
        public async Task Sem_workspace()
        {
            var gitlab = GitLabOk();
            var (servico, _) = Criar(gitlab);

            var r = await servico.ObterAsync(App(workspace: null), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.SemWorkspace);
            r.Fatias.Should().BeEmpty();
            gitlab.ReceivedCalls().Should().BeEmpty();
        }

        [Fact, DisplayName("15: uma Aplicação com falha não esconde as outras")]
        public async Task Falha_isolada()
        {
            var gitlab = GitLabOk();
            gitlab.ObterArquivoAsync("valuto", 99, Arg.Any<string>(), "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Falha(Indisponivel));
            gitlab.ListarIssuesAsync("valuto", 99, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<IssueGitLabDto>>.Falha(Indisponivel));
            gitlab.ListarMergeRequestsAsync("valuto", 99, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Falha(Indisponivel));
            var (servico, _) = Criar(gitlab);

            var rs = await servico.ObterAsync([App("a", 99), App("b"), App("c", null)], Ct);

            rs.Select(r => (r.Chave, r.Situacao)).Should().Equal(
                ("a", SituacaoDoPainel.Falha), ("b", SituacaoDoPainel.Ok), ("c", SituacaoDoPainel.SemWorkspace));
        }

        [Fact, DisplayName("13: falha na leitura não deixa a trava presa: a chegada seguinte tenta de novo")]
        public async Task Falha_libera_a_trava()
        {
            var gitlab = GitLabOk();
            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<IssueGitLabDto>>.Falha(Indisponivel));
            var (servico, _) = Criar(gitlab);
            (await servico.ObterAsync(App(), Ct)).Situacao.Should().Be(SituacaoDoPainel.Falha);

            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso([]));
            var r = await servico.ObterAsync(App(), Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Ok);
        }
    }
}
