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
    // P2b.3, critério 6: a consulta periódica da pipeline do `abrir-review`. Confirma no success, falha em
    // failed/canceled e no prazo esgotado (cancelando a pipeline). GitLab é falso; o RegistrarProgressoDaAcao é o real.
    [Trait("Fatia", "P2b")]
    public class ConsultaDePipelinesTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long AppId = 5;
        private const long PessoaId = 9;
        private const long ProjetoFront = 72;
        private const long ProjetoGitLabFront = 7202;
        private const string Grupo = "valuto";
        private const long PipelineId = 1234;
        private static readonly DateTime Agora = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAplicacaoRepositorio _aplicacoes = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IDescarteDoPainel _painel = Substitute.For<IDescarteDoPainel>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly IOpcoesDeRevisao _opcoes = Substitute.For<IOpcoesDeRevisao>();
        private readonly FakeLogger<ConsultaDePipelines> _log = new();
        private readonly List<AcaoBotEntity> _acoesEmExecucao = [];
        private readonly List<AcaoBotEntity> _todas = [];

        public ConsultaDePipelinesTests()
        {
            _acoes.ObterPorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(c => _todas.FirstOrDefault(a => a.PublicId == c.Arg<Guid>()));
            _acoes.ObterAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(c => _todas.FirstOrDefault(a => a.Id == c.Arg<long>()));
            _acoes.ListarDaFatiaAsync(AppId, "P2", Arg.Any<CancellationToken>()).Returns(_ => _todas.ToList());
            _acoes.ListarEmExecucaoPorTipoAsync(TipoAcaoBot.AbrirReview, Arg.Any<CancellationToken>())
                .Returns(_ => _acoesEmExecucao.ToList());
            _acesso.ObterPublicIdAsync(AppId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
            _uow.SaveChangesAsync().Returns(1);
            _opcoes.PrazoMinutos.Returns(30);

            var app = AplicacaoEntity.Criar("Valuto", "VAL", 10, Grupo).Value;
            Definir(app, AppId);
            _aplicacoes.ObterAsync(AppId, Arg.Any<CancellationToken>()).Returns(app);
            var projeto = Definir(ProjetoEntity.Criar(AppId, ProjetoGitLabFront, "valuto/front", "front", Stack.Angular), ProjetoFront);
            _projetos.ProjetosDaAplicacaoAsync(AppId, Arg.Any<CancellationToken>()).Returns([projeto]);
        }

        private static T Definir<T>(T e, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);
            return e;
        }

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private ConsultaDePipelines Consulta()
        {
            var relogio = new RelogioFixo();
            var progresso = new RegistrarProgressoDaAcao(_acoes, _acesso, _uow, _auditoria, _painel, relogio);
            var contexto = new ContextoGitLabDaAcao(_aplicacoes, _projetos, new FakeLogger<ContextoGitLabDaAcao>());
            var conclusao = new ConclusaoDaAcaoBot(_acoes, _aplicacoes, _projetos, _gitlab, progresso, _publicador,
                new FakeLogger<ConclusaoDaAcaoBot>());
            return new ConsultaDePipelines(_acoes, contexto, _gitlab, conclusao, _opcoes, relogio, _log);
        }

        private AcaoBotEntity EmExecucao(long id, DateTime iniciadaEm, long? pipelineId = PipelineId)
        {
            var acao = AcaoBotEntity.Criar(AppId, "P2", ProjetoFront, 3, TipoAcaoBot.AbrirReview, 1, null, PessoaId, null, iniciadaEm.AddMinutes(-1)).Value;
            Definir(acao, id);
            if (pipelineId is not null)
                acao.RegistrarPipeline(pipelineId.Value);
            acao.Iniciar(iniciadaEm);
            _acoesEmExecucao.Add(acao);
            _todas.Add(acao);
            return acao;
        }

        private void Pipeline(string status)
            => _gitlab.ObterPipelineAsync(Grupo, ProjetoGitLabFront, PipelineId, Arg.Any<CancellationToken>())
                .Returns(Result<PipelineGitLabDto>.Sucesso(new(PipelineId, status)));

        private void PipelineFalha(Erro erro)
            => _gitlab.ObterPipelineAsync(Grupo, ProjetoGitLabFront, PipelineId, Arg.Any<CancellationToken>())
                .Returns(Result<PipelineGitLabDto>.Falha(erro));

        private void Cancela()
            => _gitlab.CancelarPipelineAsync(Grupo, ProjetoGitLabFront, PipelineId, Arg.Any<CancellationToken>())
                .Returns(Result<PipelineGitLabDto>.Sucesso(new(PipelineId, "canceled")));

        private static Erro Erro(string codigo, TipoErro tipo = TipoErro.Failure) => new(codigo, "erro", tipo);

        [Fact, DisplayName("6: pipeline em success confirma a Ação abrir-review")]
        public async Task Pipeline_success_confirma()
        {
            var acao = EmExecucao(1, Agora.AddMinutes(-5));
            Pipeline("success");

            await Consulta().ExecutarAsync(Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _gitlab.DidNotReceiveWithAnyArgs().CancelarPipelineAsync(default, default, default, default);
        }

        [Theory, DisplayName("6: pipeline failed ou canceled faz a Ação falhar em preview.pipeline-falhou")]
        [InlineData("failed")]
        [InlineData("canceled")]
        public async Task Pipeline_falha_faz_acao_falhar(string status)
        {
            var acao = EmExecucao(1, Agora.AddMinutes(-5));
            Pipeline(status);

            await Consulta().ExecutarAsync(Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be("preview.pipeline-falhou");
        }

        [Fact, DisplayName("6: passado o prazo com a pipeline ainda em andamento, falha em preview.prazo-esgotado e cancela a pipeline")]
        public async Task Prazo_esgotado_falha_e_cancela()
        {
            var acao = EmExecucao(1, Agora.AddMinutes(-31));
            Pipeline("running");
            Cancela();

            await Consulta().ExecutarAsync(Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be("preview.prazo-esgotado");
            await _gitlab.Received(1).CancelarPipelineAsync(Grupo, ProjetoGitLabFront, PipelineId, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("6: pipeline em andamento dentro do prazo não muda a Ação")]
        public async Task Em_andamento_dentro_do_prazo_nao_muda()
        {
            var acao = EmExecucao(1, Agora.AddMinutes(-5));
            Pipeline("running");

            await Consulta().ExecutarAsync(Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
            await _gitlab.DidNotReceiveWithAnyArgs().CancelarPipelineAsync(default, default, default, default);
        }

        [Fact, DisplayName("6: erro transitório ao ler a pipeline só loga e não derruba a consulta (a Ação segue executando)")]
        public async Task Erro_transitorio_nao_derruba()
        {
            var acao = EmExecucao(1, Agora.AddMinutes(-5));
            PipelineFalha(Erro("gitlab.indisponivel"));

            await Consulta().ExecutarAsync(Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }
    }
}
