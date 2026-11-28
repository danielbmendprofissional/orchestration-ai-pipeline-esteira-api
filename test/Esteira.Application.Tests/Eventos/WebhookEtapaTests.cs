using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Entregas;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Domain.Entregas;
using Esteira.Domain.Fatias;
using MassTransit;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Esteira.Application.Tests.Eventos
{
    // E1b.3 (spec 02-tela-da-entrega.md, criterios 5, 6, 7, 8, 10 e 15): o webhook aplica o status da pipeline a Etapa.
    // Portas falsas; a corrida real com PostgreSQL esta em Esteira.Integration.Tests.
    [Trait("Fatia", "E1b")]
    public class WebhookEtapaTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const string Caminho = "valuto/api";
        private const long ProjetoId = 72;
        private const long PipelineId = 9001;
        private static readonly Guid AppPublicId = Guid.NewGuid();
        private static readonly Guid ProjetoPublicId = Guid.NewGuid();
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);

        private readonly IProjetoDoWebhookQuery _projetos = Substitute.For<IProjetoDoWebhookQuery>();
        private readonly IAcaoBotRepositorio _acoes = Substitute.For<IAcaoBotRepositorio>();
        private readonly IEntregaRepositorio _entregas = Substitute.For<IEntregaRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IOpcoesDaEntrega _opcoes = Substitute.For<IOpcoesDaEntrega>();
        private readonly IPublicadorDeEventos _eventos = Substitute.For<IPublicadorDeEventos>();
        private readonly List<string> _ordem = [];
        private readonly List<EventoDeTempoReal> _publicados = [];
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public WebhookEtapaTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), ProjetoId);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);

            _opcoes.E1bLigada.Returns(true);
            _projetos.ObterAtivoAsync(Caminho, Arg.Any<CancellationToken>())
                .Returns(new ProjetoDoWebhook(ProjetoId, ProjetoPublicId, 7, AppPublicId));
            _acoes.ListarEmExecucaoPorTipoAsync(Arg.Any<TipoAcaoBot>(), Arg.Any<CancellationToken>()).Returns([]);
            _entregas.ObterEtapaPorPipelineAsync(ProjetoId, PipelineId, Arg.Any<CancellationToken>()).Returns(c =>
                Task.FromResult<EtapaDaPipeline?>(new EtapaDaPipeline(_fluxo.Etapa(EtapaDoFluxo.Qa), _fluxo.PublicId, _entrega.PublicId)));
            _uow.SaveChangesAsync().Returns(_ => { _ordem.Add("commit"); return Task.FromResult(1); });
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>()).Returns(c =>
            {
                _ordem.Add(c.Arg<EventoDeTempoReal>().Tipo);
                _publicados.Add(c.Arg<EventoDeTempoReal>());
                return Task.CompletedTask;
            });
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private sealed class Relogio : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0.AddMinutes(30));
        }

        private TratadorDeWebhookGitLab Tratador()
        {
            var progresso = new RegistrarProgressoDaAcao(_acoes, Substitute.For<IAcessoAplicacaoQuery>(), _uow,
                Substitute.For<IAuditoria>(), Substitute.For<IDescarteDoPainel>(), new Relogio(), _eventos);
            var conclusao = new ConclusaoDaAcaoBot(_acoes, Substitute.For<IAplicacaoRepositorio>(), Substitute.For<IProjetoRepositorio>(),
                Substitute.For<IGitLab>(), progresso, Substitute.For<IPublishEndpoint>(), new FakeLogger<ConclusaoDaAcaoBot>());
            return new TratadorDeWebhookGitLab(_projetos, _acoes, conclusao, Substitute.For<IDescarteDoPainel>(), _eventos,
                new FakeLogger<TratadorDeWebhookGitLab>(), _entregas, _uow, _opcoes, new Relogio());
        }

        private static string Corpo(string status, long pipelineId = PipelineId, string? finishedAt = "2026-11-27 11:00:00 UTC")
        {
            var fim = finishedAt is null ? "" : $",\"finished_at\":\"{finishedAt}\"";
            return $$$"""{"object_attributes":{"id":{{{pipelineId}}},"status":"{{{status}}}","ref":"main","sha":"abc"{{{fim}}}},"project":{"path_with_namespace":"{{{Caminho}}}"}}""";
        }

        private EtapaFluxoEntity Qa => _fluxo.Etapa(EtapaDoFluxo.Qa);

        private void Executando(EtapaDoFluxo etapa = EtapaDoFluxo.Qa)
        {
            _fluxo.Etapa(etapa).IniciarExecucao(PipelineId, T0.AddMinutes(1)).IsSuccess.Should().BeTrue();
            _entregas.ObterEtapaPorPipelineAsync(ProjetoId, PipelineId, Arg.Any<CancellationToken>()).Returns(c =>
                Task.FromResult<EtapaDaPipeline?>(new EtapaDaPipeline(_fluxo.Etapa(etapa), _fluxo.PublicId, _entrega.PublicId)));
        }

        private IEnumerable<EntregaAtualizada> Entrega() => _publicados.OfType<EntregaAtualizada>();

        [Theory, DisplayName("5: success poe a Etapa qa ou hml em aguardando-aprovacao e prd em concluido, e publica depois do commit")]
        [InlineData(EtapaDoFluxo.Qa, EstadoEtapa.AguardandoAprovacao, "qa", "aguardando-aprovacao")]
        [InlineData(EtapaDoFluxo.Hml, EstadoEtapa.AguardandoAprovacao, "hml", "aguardando-aprovacao")]
        [InlineData(EtapaDoFluxo.Prd, EstadoEtapa.Concluido, "prd", "concluido")]
        public async Task Success_AplicaEPublicaDepoisDoCommit(EtapaDoFluxo etapa, EstadoEtapa esperado, string etapaTexto, string estadoTexto)
        {
            Executando(etapa);

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();

            _fluxo.Etapa(etapa).Estado.Should().Be(esperado);
            _ordem.Should().Equal("pipeline.atualizada", "commit", "entrega.atualizada");
            Entrega().Should().ContainSingle().Which.Should().Be(
                new EntregaAtualizada(AppPublicId, _entrega.PublicId, _fluxo.PublicId, etapaTexto, estadoTexto));
        }

        [Theory, DisplayName("6: failed e canceled poem a Etapa em falhou com pipeline.falhou ou pipeline.cancelada")]
        [InlineData("failed", "pipeline.falhou")]
        [InlineData("canceled", "pipeline.cancelada")]
        public async Task FailedOuCanceled_Falhou(string status, string codigo)
        {
            Executando();

            (await Tratador().TratarAsync("Pipeline Hook", Corpo(status), Ct)).IsSuccess.Should().BeTrue();

            Qa.Estado.Should().Be(EstadoEtapa.Falhou);
            Qa.CodigoFalha.Should().Be(codigo);
            Entrega().Should().ContainSingle().Which.Estado.Should().Be("falhou");
        }

        [Theory, DisplayName("6: status intermediario nao muda a Etapa, nao grava e nao publica entrega.atualizada")]
        [InlineData("created")]
        [InlineData("pending")]
        [InlineData("running")]
        [InlineData("manual")]
        public async Task Intermediario_SemMudanca(string status)
        {
            Executando();

            (await Tratador().TratarAsync("Pipeline Hook", Corpo(status), Ct)).IsSuccess.Should().BeTrue();

            Qa.Estado.Should().Be(EstadoEtapa.Executando);
            _ordem.Should().NotContain("commit");
            Entrega().Should().BeEmpty();
        }

        [Fact, DisplayName("7: pipeline que nenhuma Etapa do Projeto guardou nao altera Etapa, so consulta por id da pipeline e Projeto (nunca SHA)")]
        public async Task SemEtapaCorrespondente_NadaMuda()
        {
            Executando();

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success", pipelineId: 5555), Ct)).IsSuccess.Should().BeTrue();

            Qa.Estado.Should().Be(EstadoEtapa.Executando);
            await _entregas.Received(1).ObterEtapaPorPipelineAsync(ProjetoId, 5555, Arg.Any<CancellationToken>());
            await _entregas.DidNotReceive().ObterEtapaPorPipelineAsync(ProjetoId, PipelineId, Arg.Any<CancellationToken>());
            Entrega().Should().BeEmpty();
            _ordem.Should().NotContain("commit");
        }

        [Fact, DisplayName("8: evento mais velho que o ultimo aplicado e descartado, sem regredir o estado nem publicar")]
        public async Task EventoMaisVelho_Descartado()
        {
            Executando();
            await Tratador().TratarAsync("Pipeline Hook", Corpo("success", finishedAt: "2026-11-27 11:00:00 UTC"), Ct);
            _ordem.Clear();
            _publicados.Clear();

            var r = await Tratador().TratarAsync("Pipeline Hook", Corpo("failed", finishedAt: "2026-11-27 10:30:00 UTC"), Ct);

            r.IsSuccess.Should().BeTrue();
            Qa.Estado.Should().Be(EstadoEtapa.AguardandoAprovacao);
            _ordem.Should().NotContain("commit");
            Entrega().Should().BeEmpty();
        }

        [Fact, DisplayName("8: sem finished_at vale a hora do recebimento")]
        public async Task SemFinishedAt_UsaORelogio()
        {
            Executando();

            await Tratador().TratarAsync("Pipeline Hook", Corpo("success", finishedAt: null), Ct);

            Qa.Estado.Should().Be(EstadoEtapa.AguardandoAprovacao);
            Qa.UltimoEventoEm.Should().Be(T0.AddMinutes(30));
        }

        [Fact, DisplayName("flag desligada: o webhook segue como antes, nao consulta nem grava Etapa")]
        public async Task FlagDesligada_NaoToca()
        {
            _opcoes.E1bLigada.Returns(false);
            Executando();

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();

            await _entregas.DidNotReceiveWithAnyArgs().ObterEtapaPorPipelineAsync(default, default, default);
            Qa.Estado.Should().Be(EstadoEtapa.Executando);
            Entrega().Should().BeEmpty();
            _publicados.OfType<PipelineAtualizada>().Should().ContainSingle();
        }

        [Fact, DisplayName("10: conflito de concorrencia recarrega o vencedor e reaplica uma vez; publica uma vez")]
        public async Task Conflito_RecarregaEReaplica()
        {
            Executando();
            var chamadas = 0;
            _uow.SaveChangesAsync().Returns(_ =>
            {
                _ordem.Add("commit");
                return ++chamadas == 1 ? throw new ConcorrenciaOtimistaException() : Task.FromResult(1);
            });
            // O Reload real devolve a Etapa ao estado do banco (o vencedor so mexeu no pipeline guardado, nao no estado): simula-o.
            _entregas.RecarregarAsync(Arg.Any<EtapaFluxoEntity>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                typeof(EtapaFluxoEntity).GetProperty(nameof(EtapaFluxoEntity.Estado))!.SetValue(Qa, EstadoEtapa.Executando);
                typeof(EtapaFluxoEntity).GetProperty(nameof(EtapaFluxoEntity.UltimoEventoEm))!.SetValue(Qa, T0.AddMinutes(1));
                return Task.CompletedTask;
            });
            _ordem.Clear(); // re-armar o NSubstitute executa o callback anterior: tira esse ruido de configuracao

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();

            await _entregas.Received(1).RecarregarAsync(Arg.Any<EtapaFluxoEntity>(), Arg.Any<CancellationToken>());
            Qa.Estado.Should().Be(EstadoEtapa.AguardandoAprovacao);
            _ordem.Count(o => o == "commit").Should().Be(2);
            Entrega().Should().ContainSingle();
        }

        [Fact, DisplayName("10: o vencedor ja gravou o mesmo valor: idempotente, sem erro e sem publicar de novo")]
        public async Task Conflito_MesmoValor_Idempotente()
        {
            Executando();
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ConcorrenciaOtimistaException());
            _entregas.RecarregarAsync(Arg.Any<EtapaFluxoEntity>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                // O vencedor (outro webhook) ja pos a Etapa em aguardando-aprovacao com o mesmo evento.
                Qa.AplicarStatusDaPipeline("success", DateTime.Parse("2026-11-27T11:00:00Z").ToUniversalTime());
                return Task.CompletedTask;
            });

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();

            Qa.Estado.Should().Be(EstadoEtapa.AguardandoAprovacao);
            Entrega().Should().BeEmpty();
        }

        [Fact, DisplayName("10: conflito repetido nunca vira erro para quem chamou")]
        public async Task ConflitoRepetido_SemErro()
        {
            Executando();
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ConcorrenciaOtimistaException());

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();

            Entrega().Should().BeEmpty();
        }

        [Fact, DisplayName("Etapa fora da tabela (ja concluida) e no-op: o webhook nunca devolve erro por causa da Etapa")]
        public async Task TransicaoInvalida_NoOp()
        {
            Executando(EtapaDoFluxo.Prd);
            await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct);
            _ordem.Clear();
            _publicados.Clear();

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("failed", finishedAt: "2026-11-27 12:00:00 UTC"), Ct)).IsSuccess.Should().BeTrue();

            _fluxo.Etapa(EtapaDoFluxo.Prd).Estado.Should().Be(EstadoEtapa.Concluido);
            Entrega().Should().BeEmpty();
        }

        [Fact, DisplayName("15: falha ao publicar entrega.atualizada nao derruba o webhook")]
        public async Task FalhaAoPublicar_NaoDerruba()
        {
            Executando();
            _eventos.PublicarAsync(Arg.Is<EventoDeTempoReal>(e => e is EntregaAtualizada), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new InvalidOperationException("redis fora"));

            (await Tratador().TratarAsync("Pipeline Hook", Corpo("success"), Ct)).IsSuccess.Should().BeTrue();
        }
    }
}
