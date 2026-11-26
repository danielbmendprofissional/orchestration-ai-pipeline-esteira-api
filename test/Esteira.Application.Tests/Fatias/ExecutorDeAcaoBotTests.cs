using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2b.2, critérios 1, 2, 3, 10 e 11: o executor lê a MR como o bot do grupo, executa aprovar/reprovar e leva a Ação
    // a confirmada ou falhou pelo RegistrarProgressoDaAcao. GitLab, repositórios e publicador são falsos; o
    // RegistrarProgressoDaAcao é o de verdade, para o estado da Ação ser o que os testes conferem.
    [Trait("Fatia", "P2b")]
    public class ExecutorDeAcaoBotTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private const long PessoaId = 9;
        private const long ProjetoApi = 71;
        private const long ProjetoGitLabApi = 7101;
        private const long ProjetoFront = 72;
        private const long ProjetoGitLabFront = 7202;
        private const string Grupo = "valuto";
        private static readonly DateTime Agora = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAplicacaoRepositorio _aplicacoes = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IPessoaRepositorio _pessoas = Substitute.For<IPessoaRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IDescarteDoPainel _painel = Substitute.For<IDescarteDoPainel>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly FakeLogger<ExecutorDeAcaoBot> _log = new();
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<AcaoBotEntity> _todas = [];

        public ExecutorDeAcaoBotTests()
        {
            _acoes.ObterPorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(c => _todas.FirstOrDefault(a => a.PublicId == c.Arg<Guid>()));
            _acoes.ObterAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(c => _todas.FirstOrDefault(a => a.Id == c.Arg<long>()));
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns(_ => _todas.ToList());
            _acesso.ObterPublicIdAsync(AppId, Arg.Any<CancellationToken>()).Returns(_appPublicId);
            _uow.SaveChangesAsync().Returns(1);

            var app = AplicacaoEntity.Criar("Valuto", "VAL", 10, Grupo).Value;
            Definir(app, AppId);
            _aplicacoes.ObterAsync(AppId, Arg.Any<CancellationToken>()).Returns(app);
            _projetos.ProjetosDaAplicacaoAsync(AppId, Arg.Any<CancellationToken>())
                .Returns([Projeto(ProjetoApi, ProjetoGitLabApi, "valuto/api"), Projeto(ProjetoFront, ProjetoGitLabFront, "valuto/front")]);
            _pessoas.ObterPorIdAsync(PessoaId, Arg.Any<CancellationToken>())
                .Returns(PessoaEntity.Criar("sub-ana", "Ana", "ana@x.com", Agora));
        }

        private static T Definir<T>(T e, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);
            return e;
        }

        private static ProjetoEntity Projeto(long id, long gitLabId, string caminho)
            => Definir(ProjetoEntity.Criar(AppId, gitLabId, caminho, caminho, Stack.Dotnet), id);

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private ExecutorDeAcaoBot Executor()
        {
            var relogio = new RelogioFixo();
            var progresso = new RegistrarProgressoDaAcao(_acoes, _acesso, _uow, _auditoria, _painel, relogio);
            var contexto = new ContextoGitLabDaAcao(_aplicacoes, _projetos, new FakeLogger<ContextoGitLabDaAcao>());
            var conclusao = new ConclusaoDaAcaoBot(
                _acoes, _aplicacoes, _projetos, _gitlab, progresso, _publicador, new FakeLogger<ConclusaoDaAcaoBot>());
            return new ExecutorDeAcaoBot(_acoes, _pessoas, _gitlab, progresso, contexto, conclusao, _uow, relogio, _log);
        }

        private AcaoBotEntity Acao(
            long id, TipoAcaoBot tipo = TipoAcaoBot.Aprovar, long projeto = ProjetoApi, long mrIid = 3,
            EstadoAcaoBot estado = EstadoAcaoBot.Pedida, long? dependeDe = null, string comentario = null)
        {
            var acao = AcaoBotEntity.Criar(
                AppId, "P2", projeto, mrIid, tipo, 1, dependeDe, PessoaId,
                tipo == TipoAcaoBot.Reprovar ? comentario ?? "não gostei" : null, Agora.AddMinutes(-10)).Value;
            Definir(acao, id);
            switch (estado)
            {
                case EstadoAcaoBot.Executando: acao.Iniciar(Agora.AddMinutes(-5)); break;
                case EstadoAcaoBot.Confirmada: acao.Iniciar(Agora.AddMinutes(-5)); acao.Confirmar(Agora.AddMinutes(-4)); break;
                case EstadoAcaoBot.Falhou: acao.Iniciar(Agora.AddMinutes(-5)); acao.Falhar("x", Agora.AddMinutes(-4)); break;
                case EstadoAcaoBot.Cancelada: acao.Cancelar(Agora.AddMinutes(-5)); break;
            }

            _todas.Add(acao);
            return acao;
        }

        private void Mr(long gitLabId, string estado, string pipeline, long iid = 3)
            => _gitlab.ObterMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(iid, estado, "feature/p2b-x", "sha1", pipeline)));

        private void MrFalha(long gitLabId, Erro erro, long iid = 3)
            => _gitlab.ObterMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Falha(erro));

        private void Mergeia(long gitLabId, string estado = "merged", long iid = 3)
            => _gitlab.MergearMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(iid, estado, "feature/p2b-x", "sha1", "success")));

        private void Notas(long gitLabId, long iid = 3, params string[] corpos)
            => _gitlab.ListarNotasMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<string>>.Sucesso(corpos));

        private void Comenta(long gitLabId, long iid = 3, Action<string> captura = null)
            => _gitlab.ComentarMergeRequestAsync(Grupo, gitLabId, iid, Arg.Do<string>(c => captura?.Invoke(c)), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Sucesso(999));

        private void ComentaFalha(long gitLabId, Erro erro, long iid = 3)
            => _gitlab.ComentarMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Falha(erro));

        private void Fecha(long gitLabId, string estado = "closed", long iid = 3)
            => _gitlab.FecharMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(iid, estado, "feature/p2b-x", "sha1", "success")));

        private static Erro Erro(string codigo, TipoErro tipo = TipoErro.Failure) => new(codigo, "erro", tipo);

        // ---- Critério 1: aprovar mergeia, comenta e confirma ----

        [Fact, DisplayName("1: aprovar com MR aberta e CI verde mergeia, comenta com o nome do humano e confirma")]
        public async Task Aprovar_mergeia_comenta_confirma()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabApi, "opened", "success");
            Mergeia(ProjetoGitLabApi);
            Notas(ProjetoGitLabApi);
            string corpo = null;
            Comenta(ProjetoGitLabApi, captura: c => corpo = c);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            acao.CodigoErro.Should().BeNull();

            // Merge pelo método da porta que já manda merge commit, sem squash e sem apagar a branch (AcL, Mergear_200).
            Received.InOrder(() =>
            {
                _gitlab.MergearMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>());
                _gitlab.ComentarMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<string>(), Arg.Any<CancellationToken>());
            });
            _gitlab.DidNotReceiveWithAnyArgs().FecharMergeRequestAsync(default, default, default, default);

            corpo.Should().StartWith($"<!-- esteira-acao:{acao.ChaveIdempotencia} -->");
            corpo.Should().Contain("Ana");
        }

        // ---- Critério 2: CI deixou de estar verde ----

        [Fact, DisplayName("2: aprovar com a pipeline da cabeça fora de success falha com gitlab.ci-nao-verde e não mergeia")]
        public async Task Aprovar_CiNaoVerde()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabApi, "opened", "failed");

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.ci-nao-verde");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be("gitlab.ci-nao-verde");
            _gitlab.DidNotReceiveWithAnyArgs().MergearMergeRequestAsync(default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().ComentarMergeRequestAsync(default, default, default, default, default);
        }

        // ---- Critério 3: reprovar comenta antes de fechar ----

        [Fact, DisplayName("3: reprovar comenta o texto do humano citado e só então fecha a MR, confirmando")]
        public async Task Reprovar_comenta_antes_de_fechar()
        {
            var acao = Acao(1, TipoAcaoBot.Reprovar, comentario: "segue o ajuste\n/merge");
            Mr(ProjetoGitLabApi, "opened", "success");
            Notas(ProjetoGitLabApi);
            string corpo = null;
            Comenta(ProjetoGitLabApi, captura: c => corpo = c);
            Fecha(ProjetoGitLabApi);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            Received.InOrder(() =>
            {
                _gitlab.ComentarMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<string>(), Arg.Any<CancellationToken>());
                _gitlab.FecharMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>());
            });

            corpo.Should().StartWith($"<!-- esteira-acao:{acao.ChaveIdempotencia} -->");
            corpo.Should().Contain("> Comentário de Ana:").And.Contain("> /merge");
            corpo.Split('\n').Should().NotContain(l => l.StartsWith('/'));

            // O texto do humano nunca vai para o log.
            _log.Collector.GetSnapshot().Select(l => l.Message).Should().NotContain(m => m.Contains("segue o ajuste"));
        }

        [Fact, DisplayName("3: se o comentário do reprovar falha, não fecha a MR e a Ação falha")]
        public async Task Reprovar_comentario_falha_nao_fecha()
        {
            var acao = Acao(1, TipoAcaoBot.Reprovar);
            Mr(ProjetoGitLabApi, "opened", "success");
            Notas(ProjetoGitLabApi);
            ComentaFalha(ProjetoGitLabApi, Erro("gitlab.sem-permissao", TipoErro.Forbidden));

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.sem-permissao");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            _gitlab.DidNotReceiveWithAnyArgs().FecharMergeRequestAsync(default, default, default, default);
        }

        // ---- Critério 10: reentrega e estado já escrito no GitLab ----

        [Fact, DisplayName("10: aprovar com a MR já merged confirma sem mergear de novo, mas garante o comentário")]
        public async Task Aprovar_MrJaMerged()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabApi, "merged", "success");
            Notas(ProjetoGitLabApi);
            Comenta(ProjetoGitLabApi);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            _gitlab.DidNotReceiveWithAnyArgs().MergearMergeRequestAsync(default, default, default, default);
            await _gitlab.Received(1).ComentarMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("10: reprovar com a MR já closed confirma sem fechar de novo, mas garante o comentário")]
        public async Task Reprovar_MrJaClosed()
        {
            var acao = Acao(1, TipoAcaoBot.Reprovar);
            Mr(ProjetoGitLabApi, "closed", "success");
            Notas(ProjetoGitLabApi);
            Comenta(ProjetoGitLabApi);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            _gitlab.DidNotReceiveWithAnyArgs().FecharMergeRequestAsync(default, default, default, default);
            await _gitlab.Received(1).ComentarMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("10: marcador da Ação já está nas notas da MR: não duplica o comentário")]
        public async Task Marcador_ja_presente_nao_duplica()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabApi, "opened", "success");
            Mergeia(ProjetoGitLabApi);
            Notas(ProjetoGitLabApi, 3, "comentário antigo", $"<!-- esteira-acao:{acao.ChaveIdempotencia} -->\n> Aprovado por Ana.");

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            _gitlab.DidNotReceiveWithAnyArgs().ComentarMergeRequestAsync(default, default, default, default, default);
        }

        [Fact, DisplayName("10: a mesma mensagem de novo numa Ação já confirmada não escreve no GitLab nem salva")]
        public async Task Reentrega_de_acao_confirmada_nao_escreve()
        {
            var acao = Acao(1, estado: EstadoAcaoBot.Confirmada);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.JaConcluida);
            _gitlab.DidNotReceiveWithAnyArgs().ObterMergeRequestAsync(default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().ComentarMergeRequestAsync(default, default, default, default, default);
            await _uow.DidNotReceive().SaveChangesAsync();
            await _publicador.DidNotReceiveWithAnyArgs().Publish(Arg.Any<AcaoBotPedida>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("10: erro transitório do GitLab devolve transitório e a Ação segue executando (o consumer reentrega)")]
        public async Task Erro_transitorio_nao_falha()
        {
            var acao = Acao(1);
            MrFalha(ProjetoGitLabApi, Erro("gitlab.indisponivel"));

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Transitoria);
            r.CodigoErro.Should().Be("gitlab.indisponivel");
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        // ---- Critério 11: Ação do front espera a da API ----

        [Fact, DisplayName("11: Ação do front não executa nem escreve enquanto a da API não estiver confirmada")]
        public async Task Front_espera_api()
        {
            var api = Acao(1, estado: EstadoAcaoBot.Executando);
            var front = Acao(2, projeto: ProjetoFront, mrIid: 8, dependeDe: api.Id);

            var r = await Executor().ExecutarAsync(front.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.AguardandoDependencia);
            front.Estado.Should().Be(EstadoAcaoBot.Pedida);
            _gitlab.DidNotReceiveWithAnyArgs().ObterMergeRequestAsync(default, default, default, default);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("11: quando a Ação da API confirma, a do front é republicada e passa a executar")]
        public async Task Front_executa_depois_da_api_confirmada()
        {
            var api = Acao(1, estado: EstadoAcaoBot.Executando);
            var front = Acao(2, projeto: ProjetoFront, mrIid: 8, dependeDe: api.Id);
            Mr(ProjetoGitLabFront, "opened", "success", iid: 8);
            Mergeia(ProjetoGitLabFront, iid: 8);
            Notas(ProjetoGitLabFront, 8);
            Comenta(ProjetoGitLabFront, 8);

            var aindaEspera = await Executor().ExecutarAsync(front.PublicId, Ct);
            aindaEspera.Situacao.Should().Be(SituacaoDaExecucao.AguardandoDependencia);

            // A API confirma e publica a mensagem da Ação do front ainda pedida (a retomada).
            Mr(ProjetoGitLabApi, "opened", "success");
            Mergeia(ProjetoGitLabApi);
            Notas(ProjetoGitLabApi);
            Comenta(ProjetoGitLabApi);
            var confirmada = await Executor().ExecutarAsync(api.PublicId, Ct);

            confirmada.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            api.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _publicador.Received(1).Publish(
                Arg.Is<AcaoBotPedida>(m => m.AcaoId == front.PublicId), Arg.Any<CancellationToken>());

            var executada = await Executor().ExecutarAsync(front.PublicId, Ct);

            executada.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            front.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _gitlab.Received(1).MergearMergeRequestAsync(Grupo, ProjetoGitLabFront, 8, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("11: se a Ação da API falha, a do front fica cancelada e não escreve no GitLab")]
        public async Task Api_falha_cancela_o_front()
        {
            var api = Acao(1, TipoAcaoBot.Reprovar, estado: EstadoAcaoBot.Executando);
            var front = Acao(2, TipoAcaoBot.Reprovar, projeto: ProjetoFront, mrIid: 8, dependeDe: api.Id);
            Mr(ProjetoGitLabApi, "opened", "success");
            Notas(ProjetoGitLabApi);
            ComentaFalha(ProjetoGitLabApi, Erro("gitlab.sem-permissao", TipoErro.Forbidden));

            var r = await Executor().ExecutarAsync(api.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            api.Estado.Should().Be(EstadoAcaoBot.Falhou);
            front.Estado.Should().Be(EstadoAcaoBot.Cancelada);

            var rFront = await Executor().ExecutarAsync(front.PublicId, Ct);

            rFront.Situacao.Should().Be(SituacaoDaExecucao.JaConcluida);
            // A Ação do front nunca foi ao GitLab (a leitura/escrita registrada é só a da API).
            await _gitlab.DidNotReceive().ObterMergeRequestAsync(Grupo, ProjetoGitLabFront, 8, Arg.Any<CancellationToken>());
            _gitlab.DidNotReceiveWithAnyArgs().FecharMergeRequestAsync(default, default, default, default);
        }

        // ---- P2b.4, critério 9: retry das falhas transitórias e códigos de erro definitivos ----

        [Fact, DisplayName("9: transitório antes do limite devolve transitório e a Ação segue executando")]
        public async Task Transitorio_antes_do_limite_repete()
        {
            var acao = Acao(1);
            MrFalha(ProjetoGitLabApi, Erro("gitlab.indisponivel"));

            var r = await Executor().ExecutarAsync(acao.PublicId, 2, 3, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Transitoria);
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        [Theory, DisplayName("9: transitório na última tentativa (rede/5xx ou 429) vira falhou com gitlab.indisponivel e descarta o instantâneo")]
        [InlineData("gitlab.indisponivel", TipoErro.Failure)]
        [InlineData("gitlab.limite-de-requisicoes", TipoErro.TooManyRequests)]
        public async Task Transitorio_esgotado_falha_com_indisponivel(string codigo, TipoErro tipo)
        {
            var acao = Acao(1);
            MrFalha(ProjetoGitLabApi, Erro(codigo, tipo));

            var r = await Executor().ExecutarAsync(acao.PublicId, 3, 3, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.indisponivel");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be("gitlab.indisponivel");
            await _painel.Received(1).DescartarAsync(_appPublicId.ToString(), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("9: erro definitivo não repete: falha na primeira tentativa e grava o código")]
        [InlineData("gitlab.sem-permissao", TipoErro.Forbidden)]
        [InlineData("gitlab.mr-nao-encontrada", TipoErro.NotFound)]
        [InlineData("gitlab.merge-recusado", TipoErro.Conflict)]
        public async Task Definitivo_nao_repete(string codigo, TipoErro tipo)
        {
            var acao = Acao(1);
            MrFalha(ProjetoGitLabApi, Erro(codigo, tipo));

            var r = await Executor().ExecutarAsync(acao.PublicId, 1, 3, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be(codigo);
        }

        [Fact, DisplayName("9: MR fechada no aprovar não repete e grava gitlab.mr-nao-esta-aberta")]
        public async Task Mr_fechada_grava_codigo()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabApi, "closed", "success");

            var r = await Executor().ExecutarAsync(acao.PublicId, 1, 3, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            acao.CodigoErro.Should().Be("gitlab.mr-nao-esta-aberta");
        }

        // ---- Capacidade de reabrir (sem rota nem TipoAcaoBot: o executor só a oferece) ----

        private void Reabre(long gitLabId, string estado = "opened", long iid = 3)
            => _gitlab.ReabrirMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(iid, estado, "feature/p2b-x", "sha1", "success")));

        [Fact, DisplayName("reabrir: MR closed é reaberta e a Ação confirma")]
        public async Task Reabrir_MrFechada()
        {
            var acao = Acao(1, TipoAcaoBot.AbrirReview, estado: EstadoAcaoBot.Executando);
            Mr(ProjetoGitLabApi, "closed", "success");
            Reabre(ProjetoGitLabApi);

            var r = await Executor().ReabrirMergeRequestAsync(acao, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _gitlab.Received(1).ReabrirMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("reabrir: MR já opened confirma sem chamar o GitLab para reabrir")]
        public async Task Reabrir_MrJaAberta()
        {
            var acao = Acao(1, TipoAcaoBot.AbrirReview, estado: EstadoAcaoBot.Executando);
            Mr(ProjetoGitLabApi, "opened", "success");

            var r = await Executor().ReabrirMergeRequestAsync(acao, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            _gitlab.DidNotReceiveWithAnyArgs().ReabrirMergeRequestAsync(default, default, default, default);
        }

        [Fact, DisplayName("reabrir: MR merged é erro definitivo gitlab.mr-nao-esta-fechada")]
        public async Task Reabrir_MrMergeada()
        {
            var acao = Acao(1, TipoAcaoBot.AbrirReview, estado: EstadoAcaoBot.Executando);
            Mr(ProjetoGitLabApi, "merged", "success");

            var r = await Executor().ReabrirMergeRequestAsync(acao, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.mr-nao-esta-fechada");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            _gitlab.DidNotReceiveWithAnyArgs().ReabrirMergeRequestAsync(default, default, default, default);
        }

        [Fact, DisplayName("reabrir: erro definitivo do GitLab ao reabrir falha a Ação com o código dele")]
        public async Task Reabrir_ErroDefinitivo()
        {
            var acao = Acao(1, TipoAcaoBot.AbrirReview, estado: EstadoAcaoBot.Executando);
            Mr(ProjetoGitLabApi, "closed", "success");
            _gitlab.ReabrirMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Falha(Erro("gitlab.mr-nao-encontrada", TipoErro.NotFound)));

            var r = await Executor().ReabrirMergeRequestAsync(acao, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.mr-nao-encontrada");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
        }

        [Fact, DisplayName("reabrir: erro transitório do GitLab devolve transitório e a Ação segue executando")]
        public async Task Reabrir_ErroTransitorio()
        {
            var acao = Acao(1, TipoAcaoBot.AbrirReview, estado: EstadoAcaoBot.Executando);
            Mr(ProjetoGitLabApi, "closed", "success");
            _gitlab.ReabrirMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Falha(Erro("gitlab.indisponivel")));

            var r = await Executor().ReabrirMergeRequestAsync(acao, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Transitoria);
            r.CodigoErro.Should().Be("gitlab.indisponivel");
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }
    }
}
