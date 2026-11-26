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
    // P2b.3, critérios 5, 7, 8 e 12: o `abrir-review` no executor (trigger token, pipeline e limpeza) e o
    // `preview-destroy` depois de aprovar/reprovar. GitLab é falso; o RegistrarProgressoDaAcao é o de verdade.
    [Trait("Fatia", "P2b")]
    public class ExecutorDeAcaoBotAbrirReviewTests
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
        private readonly FakeLogger<ConclusaoDaAcaoBot> _logConclusao = new();
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<AcaoBotEntity> _todas = [];

        public ExecutorDeAcaoBotAbrirReviewTests()
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
                .Returns([
                    Projeto(ProjetoApi, ProjetoGitLabApi, "valuto/api", Stack.Dotnet),
                    Projeto(ProjetoFront, ProjetoGitLabFront, "valuto/front", Stack.Angular),
                ]);
            _pessoas.ObterPorIdAsync(PessoaId, Arg.Any<CancellationToken>())
                .Returns(PessoaEntity.Criar("sub-ana", "Ana", "ana@x.com", Agora));
        }

        private static T Definir<T>(T e, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);
            return e;
        }

        private static ProjetoEntity Projeto(long id, long gitLabId, string caminho, Stack stack)
            => Definir(ProjetoEntity.Criar(AppId, gitLabId, caminho, caminho, stack), id);

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private static TimeProvider Relogio() => new RelogioFixo();

        private ExecutorDeAcaoBot Executor()
        {
            var progresso = new RegistrarProgressoDaAcao(_acoes, _acesso, _uow, _auditoria, _painel, Relogio());
            var contexto = new ContextoGitLabDaAcao(_aplicacoes, _projetos, new FakeLogger<ContextoGitLabDaAcao>());
            var conclusao = new ConclusaoDaAcaoBot(_acoes, _aplicacoes, _projetos, _gitlab, progresso, _publicador, _logConclusao);
            return new ExecutorDeAcaoBot(_acoes, _pessoas, _gitlab, progresso, contexto, conclusao, _uow, Relogio(), _log);
        }

        private AcaoBotEntity Acao(
            long id, TipoAcaoBot tipo = TipoAcaoBot.AbrirReview, long projeto = ProjetoFront, long mrIid = 3,
            EstadoAcaoBot estado = EstadoAcaoBot.Pedida, long? pipelineId = null)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", projeto, mrIid, tipo, 1, null, PessoaId, null, Agora.AddMinutes(-10)).Value;
            Definir(acao, id);
            if (pipelineId is not null)
                acao.RegistrarPipeline(pipelineId.Value);
            switch (estado)
            {
                case EstadoAcaoBot.Executando: acao.Iniciar(Agora.AddMinutes(-5)); break;
                case EstadoAcaoBot.Confirmada: acao.Iniciar(Agora.AddMinutes(-5)); acao.Confirmar(Agora.AddMinutes(-4)); break;
                case EstadoAcaoBot.Falhou: acao.Iniciar(Agora.AddMinutes(-5)); acao.Falhar("x", Agora.AddMinutes(-4)); break;
            }

            _todas.Add(acao);
            return acao;
        }

        private void Mr(long gitLabId, string estado = "opened", string pipeline = "running", long iid = 3)
            => _gitlab.ObterMergeRequestAsync(Grupo, gitLabId, iid, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(iid, estado, "feature/p2b-x", "sha1", pipeline)));

        private void Tokens(long gitLabId, params TriggerTokenListadoGitLabDto[] tokens)
            => _gitlab.ListarTriggerTokensAsync(Grupo, gitLabId, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>.Sucesso(tokens));

        private void CriaToken(long gitLabId, long id = 55, string token = "trg-NOVO")
            => _gitlab.CriarTriggerTokenAsync(Grupo, gitLabId, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<TriggerTokenGitLabDto>.Sucesso(new TriggerTokenGitLabDto(id, token)));

        private void CriaTokenFalha(long gitLabId, Erro erro)
            => _gitlab.CriarTriggerTokenAsync(Grupo, gitLabId, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<TriggerTokenGitLabDto>.Falha(erro));

        private void CriaPipeline(long gitLabId, long pipelineId, string refEsperada, Action<IReadOnlyDictionary<string, string>> captura = null)
            => _gitlab.CriarPipelinePorTriggerAsync(
                    Grupo, gitLabId, Arg.Any<string>(), refEsperada,
                    Arg.Do<IReadOnlyDictionary<string, string>>(v => captura?.Invoke(v)), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Sucesso(pipelineId));

        private void CriaPipelineFalha(long gitLabId, Erro erro)
            => _gitlab.CriarPipelinePorTriggerAsync(
                    Grupo, gitLabId, Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Falha(erro));

        private void ApagaToken(long gitLabId, long id = 55, Result resultado = null)
            => _gitlab.ApagarTriggerTokenAsync(Grupo, gitLabId, id, Arg.Any<CancellationToken>())
                .Returns(resultado ?? Result.Sucesso());

        private static Erro Erro(string codigo, TipoErro tipo = TipoErro.Failure) => new(codigo, "erro", tipo);

        // ---- Critério 5: abrir-review Angular cria trigger token, pipeline com as variáveis, apaga o token e grava o id ----

        [Fact, DisplayName("5: abrir-review Angular cria trigger token, dispara preview com as variáveis, apaga o token e grava o PipelineId")]
        public async Task AbrirReview_Angular_cria_pipeline()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabFront);
            Tokens(ProjetoGitLabFront);
            CriaToken(ProjetoGitLabFront);
            IReadOnlyDictionary<string, string> variaveis = null;
            CriaPipeline(ProjetoGitLabFront, 1234, "feature/p2b-x", v => variaveis = v);
            ApagaToken(ProjetoGitLabFront);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.AguardandoPipeline);
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
            acao.PipelineId.Should().Be(1234);
            variaveis["PIPELINE_PROCESS"].Should().Be("preview");
            variaveis["PREVIEW_FATIA"].Should().Be("p2");

            await _gitlab.Received(1).CriarTriggerTokenAsync(
                Grupo, ProjetoGitLabFront, $"esteira-acao-{acao.ChaveIdempotencia}", Arg.Any<CancellationToken>());
            Received.InOrder(() =>
            {
                _gitlab.CriarTriggerTokenAsync(Grupo, ProjetoGitLabFront, Arg.Any<string>(), Arg.Any<CancellationToken>());
                _gitlab.CriarPipelinePorTriggerAsync(Grupo, ProjetoGitLabFront, Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
                _gitlab.ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 55, Arg.Any<CancellationToken>());
            });
        }

        [Fact, DisplayName("5/12: se a pipeline falha ao ser criada, a Ação fica transitória e o token é apagado mesmo assim")]
        public async Task AbrirReview_Angular_falha_pipeline_apaga_token()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabFront);
            Tokens(ProjetoGitLabFront);
            CriaToken(ProjetoGitLabFront);
            CriaPipelineFalha(ProjetoGitLabFront, Erro("gitlab.indisponivel"));
            ApagaToken(ProjetoGitLabFront);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Transitoria);
            acao.PipelineId.Should().BeNull();
            await _gitlab.Received(1).ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 55, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("5/6: com PipelineId já gravado (retomada), não cria token nem pipeline de novo e segue aguardando")]
        public async Task AbrirReview_retomada_nao_recria_pipeline()
        {
            var acao = Acao(1, estado: EstadoAcaoBot.Executando, pipelineId: 1234);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.AguardandoPipeline);
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
            _gitlab.DidNotReceiveWithAnyArgs().CriarTriggerTokenAsync(default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().CriarPipelinePorTriggerAsync(default, default, default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().ObterMergeRequestAsync(default, default, default, default);
        }

        [Fact, DisplayName("5: MR fora de opened faz o abrir-review falhar em gitlab.mr-nao-esta-aberta sem criar token")]
        public async Task AbrirReview_mr_fechada_falha()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabFront, estado: "closed");

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Falhou);
            r.CodigoErro.Should().Be("gitlab.mr-nao-esta-aberta");
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            _gitlab.DidNotReceiveWithAnyArgs().CriarTriggerTokenAsync(default, default, default, default);
        }

        // ---- Critério 7: Projeto que não é Angular confirma sem chamar o GitLab ----

        [Fact, DisplayName("7: abrir-review de Projeto que não é Angular confirma na hora, sem chamar o GitLab")]
        public async Task AbrirReview_nao_angular_confirma_sem_gitlab()
        {
            var acao = Acao(1, projeto: ProjetoApi);

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _gitlab.DidNotReceiveWithAnyArgs().ObterMergeRequestAsync(default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().CriarTriggerTokenAsync(default, default, default, default);
            _gitlab.DidNotReceiveWithAnyArgs().CriarPipelinePorTriggerAsync(default, default, default, default, default, default);
        }

        // ---- Critério 12: limpeza dos trigger tokens esteira-acao-* com mais de uma hora ----

        [Fact, DisplayName("12: no começo do abrir-review apaga os trigger tokens esteira-acao-* com mais de uma hora")]
        public async Task AbrirReview_limpa_tokens_antigos()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabFront);
            Tokens(ProjetoGitLabFront,
                new TriggerTokenListadoGitLabDto(1, "esteira-acao-antigo", new DateTimeOffset(Agora.AddHours(-2))),
                new TriggerTokenListadoGitLabDto(2, "esteira-acao-recente", new DateTimeOffset(Agora.AddMinutes(-5))),
                new TriggerTokenListadoGitLabDto(3, "outro-antigo", new DateTimeOffset(Agora.AddHours(-3))));
            CriaToken(ProjetoGitLabFront);
            CriaPipeline(ProjetoGitLabFront, 1234, "feature/p2b-x");
            ApagaToken(ProjetoGitLabFront);

            await Executor().ExecutarAsync(acao.PublicId, Ct);

            await _gitlab.Received(1).ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 1, Arg.Any<CancellationToken>());
            await _gitlab.DidNotReceive().ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 2, Arg.Any<CancellationToken>());
            await _gitlab.DidNotReceive().ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 3, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("12: se apagar o token falha, só registra (sem o valor) e a Ação segue aguardando a pipeline")]
        public async Task AbrirReview_falha_ao_apagar_token_nao_falha_acao()
        {
            var acao = Acao(1);
            Mr(ProjetoGitLabFront);
            Tokens(ProjetoGitLabFront);
            CriaToken(ProjetoGitLabFront, token: "trg-SEGREDO-NAO-LOGAR");
            CriaPipeline(ProjetoGitLabFront, 1234, "feature/p2b-x");
            ApagaToken(ProjetoGitLabFront, resultado: Result.Falha(Erro("gitlab.indisponivel")));

            var r = await Executor().ExecutarAsync(acao.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.AguardandoPipeline);
            acao.PipelineId.Should().Be(1234);
            _log.Collector.GetSnapshot().Select(l => l.Message + l.Exception)
                .Should().NotContain(m => m!.Contains("trg-SEGREDO-NAO-LOGAR"));
            _logConclusao.Collector.GetSnapshot().Select(l => l.Message + l.Exception)
                .Should().NotContain(m => m!.Contains("trg-SEGREDO-NAO-LOGAR"));
        }

        // ---- Critério 8: preview-destroy depois de aprovar/reprovar confirmada ----

        private void AprovarMr()
        {
            Mr(ProjetoGitLabApi, "opened", "success");
            _gitlab.MergearMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>())
                .Returns(Result<MergeRequestDaAcaoGitLabDto>.Sucesso(new(3, "merged", "feature/p2b-x", "sha1", "success")));
            _gitlab.ListarNotasMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<string>>.Sucesso([]));
            _gitlab.ComentarMergeRequestAsync(Grupo, ProjetoGitLabApi, 3, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Sucesso(999));
        }

        [Fact, DisplayName("8: aprovar confirmada dispara preview-destroy na main para o Projeto Angular que teve abrir-review")]
        public async Task Aprovar_confirmada_dispara_preview_destroy()
        {
            var aprovar = Acao(1, TipoAcaoBot.Aprovar, projeto: ProjetoApi);
            Acao(2, projeto: ProjetoFront, estado: EstadoAcaoBot.Confirmada, pipelineId: 4321);
            AprovarMr();
            CriaToken(ProjetoGitLabFront, id: 77, token: "trg-destroy");
            IReadOnlyDictionary<string, string> variaveis = null;
            CriaPipeline(ProjetoGitLabFront, 999, "main", v => variaveis = v);
            ApagaToken(ProjetoGitLabFront, 77);

            var r = await Executor().ExecutarAsync(aprovar.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            aprovar.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            variaveis["PIPELINE_PROCESS"].Should().Be("preview-destroy");
            variaveis["PREVIEW_FATIA"].Should().Be("p2");
            await _gitlab.Received(1).ApagarTriggerTokenAsync(Grupo, ProjetoGitLabFront, 77, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("8: se o preview-destroy falha, a Ação aprovar continua confirmada")]
        public async Task Aprovar_preview_destroy_falha_nao_falha_acao()
        {
            var aprovar = Acao(1, TipoAcaoBot.Aprovar, projeto: ProjetoApi);
            Acao(2, projeto: ProjetoFront, estado: EstadoAcaoBot.Confirmada, pipelineId: 4321);
            AprovarMr();
            CriaTokenFalha(ProjetoGitLabFront, Erro("gitlab.sem-permissao", TipoErro.Forbidden));

            var r = await Executor().ExecutarAsync(aprovar.PublicId, Ct);

            r.Situacao.Should().Be(SituacaoDaExecucao.Concluida);
            aprovar.Estado.Should().Be(EstadoAcaoBot.Confirmada);
        }
    }
}
