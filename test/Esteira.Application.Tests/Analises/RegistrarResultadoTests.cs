using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Analises;
using Esteira.Application.Analises.Gate;
using Esteira.Application.Analises.Resultado;
using Esteira.Application.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Analises
{
    // E2.3 (spec 03-analises-e-gate.md, criterios 7, 8, 11, 14): `esteira-resultado` com portas substituidas.
    [Trait("Fatia", "E2")]
    public class RegistrarResultadoTests
    {
        private const string Caminho = "grupo/api";
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IProjetoPorCaminhoQuery _porCaminho = Substitute.For<IProjetoPorCaminhoQuery>();
        private readonly IEntregaRepositorio _entregas = Substitute.For<IEntregaRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IAplicacaoRepositorio _aplicacoes = Substitute.For<IAplicacaoRepositorio>();
        private readonly IAnaliseRepositorio _analises = Substitute.For<IAnaliseRepositorio>();
        private readonly IExcecoesVigentesQuery _excecoes = Substitute.For<IExcecoesVigentesQuery>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IPublicadorDeEventos _eventos = Substitute.For<IPublicadorDeEventos>();
        private readonly IOpcoesDaEntrega _opcoes = Substitute.For<IOpcoesDaEntrega>();
        private readonly List<AnaliseEntity> _gravadas = [];
        private readonly List<EventoDeTempoReal> _publicados = [];
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public RegistrarResultadoTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);
            _fluxo.RegistrarSha(Sha);
            _fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(555, T0);
            _fluxo.Etapa(EtapaDoFluxo.Hml).IniciarExecucao(555, T0);
            _fluxo.Etapa(EtapaDoFluxo.Prd).IniciarExecucao(555, T0);

            _opcoes.E1bLigada.Returns(true);
            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(10L);
            _entregas.ObterPorPublicIdAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(_entrega);
            _acesso.ObterPublicIdAsync(7, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
            foreach (var a in new[] { AmbienteDeploy.Qa, AmbienteDeploy.Hml, AmbienteDeploy.Producao })
                _projetos.ObterAmbienteAsync(10, a, Arg.Any<CancellationToken>()).Returns(AmbienteProjetoEntity.Criar(10, a, true).Value);
            foreach (var a in new[] { AmbientePolitica.Qa, AmbientePolitica.Hml, AmbientePolitica.Producao })
                _aplicacoes.ObterPoliticaAsync(7, a, Arg.Any<CancellationToken>()).Returns(PoliticaSeveridadeEntity.Padrao(7, a));
            _excecoes.ObterChavesCobertasAsync(default, default, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()));
            _analises.SubstituirAsync(default, default, default, default, default!, default).ReturnsForAnyArgs(c =>
            {
                _gravadas.AddRange(c.Arg<IReadOnlyList<AnaliseEntity>>());
                return Task.CompletedTask;
            });
            _uow.SaveChangesAsync().Returns(Task.FromResult(1));
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>()).Returns(c =>
            {
                _publicados.Add(c.Arg<EventoDeTempoReal>());
                return Task.CompletedTask;
            });
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private RegistrarResultadoHandler Handler()
            => new(_porCaminho, _entregas, _projetos, _aplicacoes, _analises, _excecoes, _uow, new Relogio(), _acesso, _eventos, _opcoes);

        private sealed class Relogio : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0);
        }

        private static GateAchadoCorpo Achado(string severidade) => new(severidade, "titulo-de-terceiro", "zap.xss", "src/a.cs", 3, "descricao-de-terceiro");

        private static GateAnaliseCorpo Analise(string destino = "k8s-a", string fonte = "zap", string veredito = "reprovado", string escopo = "deploy", params GateAchadoCorpo[] achados)
            => new(fonte, "ferramenta", escopo, veredito, destino, null, achados);

        private GateCorpo Corpo(string process = "fluxo", string env = "qa", params GateAnaliseCorpo[] analises)
            => new(process, env, _entrega.PublicId.ToString(), _fluxo.PublicId.ToString(), Sha, Caminho, "555", analises);

        [Fact, DisplayName("8: excesso em qa grava as Analises do Deploy por Destino, responde 200 bloqueado e leva a Etapa a bloqueada (resultado.bloqueado) com evento")]
        public async Task Qa_ExcessoBloqueiaEtapa_Responde200()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise("k8s-a", achados: Achado("high")), Analise("k8s-b", "smoke", "aprovado")]), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Decisao.Should().Be("bloqueado");
            _gravadas.Should().HaveCount(2);
            _gravadas.Should().OnlyContain(a => a.Escopo == EscopoAnalise.Deploy && a.Ambiente == AmbienteDeploy.Qa);
            _gravadas.Select(a => a.DestinoNome).Should().BeEquivalentTo(["k8s-a", "k8s-b"]);
            await _analises.Received(1).RegistrarDecisaoAsync(30, AmbienteDeploy.Qa, MomentoDoGate.Resultado,
                Arg.Is<AvaliacaoDoGate>(a => a.Motivo == MotivosDoGate.ResultadoBloqueado), 555, T0, Arg.Any<CancellationToken>());
            var etapa = _fluxo.Etapa(EtapaDoFluxo.Qa);
            etapa.Estado.Should().Be(EstadoEtapa.Bloqueada);
            etapa.CodigoFalha.Should().Be(MotivosDoGate.ResultadoBloqueado);
            _publicados.Should().ContainSingle();
        }

        [Fact, DisplayName("8: excesso em production registra, responde 200 avisou e nao bloqueia a Etapa")]
        public async Task Producao_NuncaBloqueia()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo("promocao", "production", Analise(achados: Achado("critical"))), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Decisao.Should().Be("avisou");
            await _analises.Received(1).RegistrarDecisaoAsync(30, AmbienteDeploy.Producao, MomentoDoGate.Resultado,
                Arg.Is<AvaliacaoDoGate>(a => a.Decisao == DecisaoGate.Avisou), 555, T0, Arg.Any<CancellationToken>());
            _fluxo.Etapa(EtapaDoFluxo.Prd).Estado.Should().Be(EstadoEtapa.Executando);
            _publicados.Should().BeEmpty();
        }

        [Fact, DisplayName("8: dentro da Politica responde liberado e nao mexe na Etapa")]
        public async Task Liberado()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: Achado("low"))]), Ct);

            r.Value.Decisao.Should().Be("liberado");
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Executando);
        }

        [Fact, DisplayName("8: Analise de escopo artefato no resultado responde 422 pipeline.corpo-invalido sem gravar")]
        public async Task EscopoArtefato_Recusa()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [new GateAnaliseCorpo("zap", "ferramenta", "artefato", "aprovado", null, null, [])]), Ct);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("pipeline.corpo-invalido");
            _analises.ReceivedCalls().Should().BeEmpty();
            _uow.ReceivedCalls().Should().BeEmpty();
        }

        [Fact, DisplayName("8: sem Analises responde gate.sem-analises; SHA diferente responde pipeline.sha-diferente")]
        public async Task SemAnalises_ESha()
        {
            (await Handler().ExecutarAsync(Caminho, Corpo(), Ct)).Errors.Should().ContainSingle().Which.Code.Should().Be("gate.sem-analises");
            var corpo = Corpo(analises: [Analise()]) with { Sha = new string('a', 40) };
            (await Handler().ExecutarAsync(Caminho, corpo, Ct)).Errors.Should().ContainSingle().Which.Code.Should().Be("pipeline.sha-diferente");
        }

        [Fact, DisplayName("11: falha ao publicar entrega.atualizada nao derruba o resultado")]
        public async Task FalhaAoPublicar_NaoDerruba()
        {
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException("sse"));

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: Achado("high"))]), Ct);

            r.IsSuccess.Should().BeTrue();
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Bloqueada);
        }

        [Fact, DisplayName("7: conflito de concorrencia persistente na Etapa responde 409 pipeline.conflito-de-gravacao, sem excecao")]
        public async Task ConflitoPersistente_409()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: Achado("high"))]), Ct);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("pipeline.conflito-de-gravacao");
            _analises.Received().DescartarPendentes();
        }
    }
}
