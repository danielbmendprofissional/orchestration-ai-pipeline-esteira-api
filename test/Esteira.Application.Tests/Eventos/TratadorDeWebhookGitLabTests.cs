using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Esteira.Application.Tests.Eventos
{
    // T1.3, critérios 1, 3, 4 e 6: tradução do Pipeline Hook e do Merge Request Hook, conclusão da Ação por status,
    // invalidação do guardado do Painel e conclusão dupla como no-op. Portas falsas; ConclusaoDaAcaoBot e
    // RegistrarProgressoDaAcao são os reais.
    [Trait("Fatia", "T1")]
    public class TratadorDeWebhookGitLabTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const string Caminho = "valuto/front";
        private const long AppId = 5;
        private const long ProjetoId = 72;
        private const long PipelineId = 1234;
        private static readonly Guid AppPublicId = Guid.NewGuid();
        private static readonly Guid ProjetoPublicId = Guid.NewGuid();
        private static readonly DateTime Agora = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

        private readonly IProjetoDoWebhookQuery _projetos = Substitute.For<IProjetoDoWebhookQuery>();
        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAplicacaoRepositorio _aplicacoes = Substitute.For<IAplicacaoRepositorio>();
        private readonly IProjetoRepositorio _repoProjetos = Substitute.For<IProjetoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IDescarteDoPainel _painel = Substitute.For<IDescarteDoPainel>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly IPublicadorDeEventos _eventos = Substitute.For<IPublicadorDeEventos>();
        private readonly List<AcaoBotEntity> _todas = [];

        public TratadorDeWebhookGitLabTests()
        {
            _projetos.ObterAtivoAsync(Caminho, Arg.Any<CancellationToken>())
                .Returns(new ProjetoDoWebhook(ProjetoId, ProjetoPublicId, AppId, AppPublicId));
            _acoes.ObterPorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(c => _todas.FirstOrDefault(a => a.PublicId == c.Arg<Guid>()));
            _acoes.ListarDaFatiaAsync(AppId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => _todas.ToList());
            _acoes.ListarEmExecucaoPorTipoAsync(TipoAcaoBot.AbrirReview, Arg.Any<CancellationToken>())
                .Returns(_ => _todas.Where(a => a.Estado == EstadoAcaoBot.Executando && a.Tipo == TipoAcaoBot.AbrirReview).ToList());
            _acesso.ObterPublicIdAsync(AppId, Arg.Any<CancellationToken>()).Returns(AppPublicId);
            _uow.SaveChangesAsync().Returns(1);
        }

        private sealed class RelogioFixo : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Agora);
        }

        private TratadorDeWebhookGitLab Tratador()
        {
            var progresso = new RegistrarProgressoDaAcao(_acoes, _acesso, _uow, _auditoria, _painel, new RelogioFixo(), _eventos);
            var conclusao = new ConclusaoDaAcaoBot(_acoes, _aplicacoes, _repoProjetos, _gitlab, progresso, _publicador,
                new FakeLogger<ConclusaoDaAcaoBot>());
            return new TratadorDeWebhookGitLab(_projetos, _acoes, conclusao, _painel, _eventos, new FakeLogger<TratadorDeWebhookGitLab>());
        }

        private AcaoBotEntity EmExecucao(long id = 1, long pipelineId = PipelineId, long projetoId = ProjetoId)
        {
            var acao = AcaoBotEntity.Criar(AppId, "T1", projetoId, 3, TipoAcaoBot.AbrirReview, 1, null, 9, null, Agora.AddMinutes(-6)).Value;
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(acao, id);
            acao.RegistrarPipeline(pipelineId);
            acao.Iniciar(Agora.AddMinutes(-5));
            _todas.Add(acao);
            return acao;
        }

        private static string Pipeline(string status, long id = PipelineId, string caminho = Caminho, string reff = "feature/t1-webhook-e-sse", string sha = "abc123")
            => $$$"""{"object_kind":"pipeline","object_attributes":{"id":{{{id}}},"status":"{{{status}}}","ref":"{{{reff}}}","sha":"{{{sha}}}"},"project":{"path_with_namespace":"{{{caminho}}}"}}""";

        private static string MergeRequest(string branch, string caminho = Caminho)
            => $$$"""{"object_kind":"merge_request","object_attributes":{"source_branch":"{{{branch}}}","title":"texto livre do GitLab"},"project":{"path_with_namespace":"{{{caminho}}}"}}""";

        [Fact, DisplayName("1: Pipeline Hook em success confirma a Ação abrir-review em executando")]
        public async Task Success_confirma()
        {
            var acao = EmExecucao();

            var resultado = await Tratador().TratarAsync("Pipeline Hook", Pipeline("success"), Ct);

            resultado.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
        }

        [Theory, DisplayName("1: Pipeline Hook em failed ou canceled falha a Ação em preview.pipeline-falhou")]
        [InlineData("failed")]
        [InlineData("canceled")]
        public async Task Falha_falha(string status)
        {
            var acao = EmExecucao();

            var resultado = await Tratador().TratarAsync("Pipeline Hook", Pipeline(status), Ct);

            resultado.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(EstadoAcaoBot.Falhou);
            acao.CodigoErro.Should().Be("preview.pipeline-falhou");
        }

        [Theory, DisplayName("3: status intermediário não conclui a Ação")]
        [InlineData("pending")]
        [InlineData("running")]
        [InlineData("created")]
        public async Task Intermediario_nao_conclui(string status)
        {
            var acao = EmExecucao();

            var resultado = await Tratador().TratarAsync("Pipeline Hook", Pipeline(status), Ct);

            resultado.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("1: pipelineId diferente do da Ação não conclui nada")]
        public async Task Outra_pipeline_nao_conclui()
        {
            var acao = EmExecucao(pipelineId: 999);

            await Tratador().TratarAsync("Pipeline Hook", Pipeline("success"), Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        [Fact, DisplayName("1: pipelineId igual mas de Ação de outro Projeto não conclui nada")]
        public async Task Outro_projeto_nao_conclui()
        {
            var acao = EmExecucao(projetoId: ProjetoId + 1);

            await Tratador().TratarAsync("Pipeline Hook", Pipeline("success"), Ct);

            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
        }

        [Fact, DisplayName("Pipeline Hook publica pipeline.atualizada só com identificadores e status")]
        public async Task Publica_pipeline_atualizada()
        {
            await Tratador().TratarAsync("Pipeline Hook", Pipeline("running", sha: "deadbeef"), Ct);

            await _eventos.Received(1).PublicarAsync(
                Arg.Is<EventoDeTempoReal>(e => e.Equals(new PipelineAtualizada(AppPublicId, ProjetoPublicId, PipelineId, "running", "feature/t1-webhook-e-sse", "deadbeef"))),
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("3: Projeto não cadastrado responde sucesso sem efeito algum")]
        public async Task Projeto_desconhecido_sem_efeito()
        {
            var acao = EmExecucao();

            var resultado = await Tratador().TratarAsync("Pipeline Hook", Pipeline("success", caminho: "outro/projeto"), Ct);

            resultado.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(EstadoAcaoBot.Executando);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
            await _painel.DidNotReceiveWithAnyArgs().DescartarAsync(default!, default);
        }

        [Fact, DisplayName("3: evento que a Esteira não trata é ignorado sem consultar o banco")]
        public async Task Evento_nao_tratado_ignorado()
        {
            var resultado = await Tratador().TratarAsync("Push Hook", Pipeline("success"), Ct);

            resultado.IsSuccess.Should().BeTrue();
            await _projetos.DidNotReceiveWithAnyArgs().ObterAtivoAsync(default!, default);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("Corpo que não é JSON do GitLab vira erro de validação webhook.corpo-invalido")]
        public async Task Corpo_invalido()
        {
            var resultado = await Tratador().TratarAsync("Pipeline Hook", "isto não é json", Ct);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors[0].Code.Should().Be("webhook.corpo-invalido");
            resultado.Errors[0].Type.Should().Be(TipoErro.Validation);
        }

        [Fact, DisplayName("4: Merge Request Hook apaga o guardado do Painel da Aplicação e publica fatias.mudaram com a Fatia da branch")]
        public async Task Mr_hook_descarta_e_publica()
        {
            await Tratador().TratarAsync("Merge Request Hook", MergeRequest("feature/t1-webhook-e-sse"), Ct);

            await _painel.Received(1).DescartarAsync(AppPublicId.ToString(), Arg.Any<CancellationToken>());
            await _eventos.Received(1).PublicarAsync(
                Arg.Is<EventoDeTempoReal>(e => e.Equals(new FatiasMudaram(AppPublicId, "T1"))), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("4: branch que não é de Fatia publica fatias.mudaram sem código de Fatia")]
        [InlineData("main")]
        [InlineData("hotfix/x")]
        public async Task Mr_hook_branch_fora_de_fatia(string branch)
        {
            await Tratador().TratarAsync("Merge Request Hook", MergeRequest(branch), Ct);

            await _painel.Received(1).DescartarAsync(AppPublicId.ToString(), Arg.Any<CancellationToken>());
            await _eventos.Received(1).PublicarAsync(
                Arg.Is<EventoDeTempoReal>(e => e.Equals(new FatiasMudaram(AppPublicId, null))), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("4: Merge Request Hook de Projeto não cadastrado não apaga nem publica nada")]
        public async Task Mr_hook_projeto_desconhecido()
        {
            var resultado = await Tratador().TratarAsync("Merge Request Hook", MergeRequest("feature/t1-x", "outro/projeto"), Ct);

            resultado.IsSuccess.Should().BeTrue();
            await _painel.DidNotReceiveWithAnyArgs().DescartarAsync(default!, default);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("6: o mesmo webhook duas vezes aplica uma conclusão e a segunda é no-op")]
        public async Task Conclusao_dupla_e_noop()
        {
            var acao = EmExecucao();
            var tratador = Tratador();

            var primeira = await tratador.TratarAsync("Pipeline Hook", Pipeline("success"), Ct);
            var segunda = await tratador.TratarAsync("Pipeline Hook", Pipeline("success"), Ct);

            primeira.IsSuccess.Should().BeTrue();
            segunda.IsSuccess.Should().BeTrue();
            acao.Estado.Should().Be(EstadoAcaoBot.Confirmada);
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("6: Ação já concluída por outro caminho entre a leitura e a gravação é no-op, sem erro")]
        public async Task Conflito_na_gravacao_e_noop()
        {
            EmExecucao();
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ConcorrenciaOtimistaException());

            var resultado = await Tratador().TratarAsync("Pipeline Hook", Pipeline("success"), Ct);

            resultado.IsSuccess.Should().BeTrue();
        }
    }
}
