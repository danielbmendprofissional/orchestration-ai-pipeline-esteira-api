using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Entregas
{
    // E1b.2 (spec 02-tela-da-entrega.md, critérios 2, 3 e 4): o `esteira-abrir` grava a Etapa na mesma gravação do SHA e da
    // ocupação e publica `entrega.atualizada` depois do commit. Portas substituídas.
    [Trait("Fatia", "E1b")]
    public class AbrirPipelineEtapasTests
    {
        private const string Caminho = "grupo/api";
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IProjetoPorCaminhoQuery _porCaminho = Substitute.For<IProjetoPorCaminhoQuery>();
        private readonly IEntregaRepositorio _entregas = Substitute.For<IEntregaRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IOcupacaoAmbienteRepositorio _ocupacoes = Substitute.For<IOcupacaoAmbienteRepositorio>();
        private readonly IDestinosDaPipelineQuery _destinos = Substitute.For<IDestinosDaPipelineQuery>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IPublicadorDeEventos _eventos = Substitute.For<IPublicadorDeEventos>();
        private readonly IOpcoesDaEntrega _opcoes = Substitute.For<IOpcoesDaEntrega>();
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<string> _ordem = [];
        private readonly List<EventoDeTempoReal> _publicados = [];
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public AbrirPipelineEtapasTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);

            _opcoes.E1bLigada.Returns(true);
            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(10L);
            _entregas.ObterPorPublicIdAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(_entrega);
            _acesso.ObterPublicIdAsync(7, Arg.Any<CancellationToken>()).Returns(_appPublicId);
            foreach (var a in new[] { AmbienteDeploy.Qa, AmbienteDeploy.Hml, AmbienteDeploy.Producao })
                _projetos.ObterAmbienteAsync(10, a, Arg.Any<CancellationToken>()).Returns(AmbienteProjetoEntity.Criar(10, a, true).Value);
            _ocupacoes.ReservarAsync(default, default, default, default, default).ReturnsForAnyArgs(_ =>
            {
                _ordem.Add($"reserva(qa={_fluxo.Etapa(EtapaDoFluxo.Qa).Estado},hml={_fluxo.Etapa(EtapaDoFluxo.Hml).Estado},prd={_fluxo.Etapa(EtapaDoFluxo.Prd).Estado})");
                return Task.FromResult(Result.Sucesso());
            });
            _destinos.ListarAsync(default, default, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<DestinoNaResposta>>([]));
            _uow.SaveChangesAsync().Returns(_ => { _ordem.Add("commit"); return Task.FromResult(1); });
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>()).Returns(c =>
            {
                _ordem.Add("evento");
                _publicados.Add(c.Arg<EventoDeTempoReal>());
                return Task.CompletedTask;
            });
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private AbrirPipelineHandler Handler()
            => new(_porCaminho, _entregas, _projetos, _ocupacoes, _destinos, _uow, new Relogio(), _acesso, _eventos, _opcoes);

        private AbrirPipelineEntrada Entrada(string process, string env, string? pipelineId = "555", string? por = null, string? motivo = null)
            => new(process, env, _entrega.PublicId, _fluxo.PublicId, Sha, Caminho, pipelineId, null, por, motivo, false);

        private EstadoEtapa Estado(EtapaDoFluxo e) => _fluxo.Etapa(e).Estado;

        [Fact, DisplayName("2: fluxo em qa poe a Etapa qa em executando, guarda o pipeline_id e publica entrega.atualizada depois do commit")]
        public async Task FluxoQa_ExecutandoEPublicaDepoisDoCommit()
        {
            var r = await Handler().ExecutarAsync(Entrada("fluxo", "qa"), Ct);

            r.IsSuccess.Should().BeTrue();
            Estado(EtapaDoFluxo.Qa).Should().Be(EstadoEtapa.Executando);
            _fluxo.Etapa(EtapaDoFluxo.Qa).PipelineGitLabId.Should().Be(555);
            Estado(EtapaDoFluxo.Hml).Should().Be(EstadoEtapa.NaoIniciado);
            _ordem.Should().Equal("commit", "evento");
            _publicados.Should().ContainSingle().Which.Should().Be(
                new EntregaAtualizada(_appPublicId, _entrega.PublicId, _fluxo.PublicId, "qa", "executando"));
        }

        [Fact, DisplayName("2: promocao em hml poe a Etapa hml em executando na mesma gravacao da ocupacao")]
        public async Task PromocaoHml_ExecutandoNaMesmaGravacaoDaOcupacao()
        {
            _fluxo.RegistrarSha(Sha);

            var r = await Handler().ExecutarAsync(Entrada("promocao", "hml"), Ct);

            r.IsSuccess.Should().BeTrue();
            Estado(EtapaDoFluxo.Hml).Should().Be(EstadoEtapa.Executando);
            _ordem.Should().StartWith("reserva(qa=NaoIniciado,hml=Executando,prd=NaoIniciado)");
            _ordem.Last().Should().Be("evento");
            _publicados.Should().ContainSingle().Which.Should().BeOfType<EntregaAtualizada>().Which.Etapa.Should().Be("hml");
        }

        [Fact, DisplayName("2: promocao em production poe a Etapa prd em executando")]
        public async Task PromocaoProducao_PrdExecutando()
        {
            _fluxo.RegistrarSha(Sha);

            (await Handler().ExecutarAsync(Entrada("promocao", "production"), Ct)).IsSuccess.Should().BeTrue();

            Estado(EtapaDoFluxo.Prd).Should().Be(EstadoEtapa.Executando);
            _fluxo.Etapa(EtapaDoFluxo.Prd).PipelineGitLabId.Should().Be(555);
        }

        [Theory, DisplayName("2: pipeline_id ausente ou nao numerico deixa a Etapa executando sem id")]
        [InlineData(null)]
        [InlineData("abc")]
        public async Task PipelineIdInvalido_ExecutandoSemId(string? pipelineId)
        {
            (await Handler().ExecutarAsync(Entrada("fluxo", "qa", pipelineId), Ct)).IsSuccess.Should().BeTrue();

            Estado(EtapaDoFluxo.Qa).Should().Be(EstadoEtapa.Executando);
            _fluxo.Etapa(EtapaDoFluxo.Qa).PipelineGitLabId.Should().BeNull();
        }

        [Fact, DisplayName("3: Atalho poe qa e hml em pulada com quem ordenou e o motivo, prd em executando, tudo antes do commit da reserva")]
        public async Task Atalho_PulaQaEHmlEExecutaPrd()
        {
            var r = await Handler().ExecutarAsync(Entrada("fluxo", "production", por: "fulano", motivo: "urgente"), Ct);

            r.IsSuccess.Should().BeTrue();
            foreach (var e in new[] { EtapaDoFluxo.Qa, EtapaDoFluxo.Hml })
            {
                Estado(e).Should().Be(EstadoEtapa.Pulada);
                _fluxo.Etapa(e).PuladaPor.Should().Be("fulano");
                _fluxo.Etapa(e).PuladaMotivo.Should().Be("urgente");
            }
            Estado(EtapaDoFluxo.Prd).Should().Be(EstadoEtapa.Executando);
            _ordem.Should().StartWith("reserva(qa=Pulada,hml=Pulada,prd=Executando)");
            _publicados.Cast<EntregaAtualizada>().Select(e => (e.Etapa, e.Estado))
                .Should().BeEquivalentTo([("qa", "pulada"), ("hml", "pulada"), ("prd", "executando")]);
        }

        [Fact, DisplayName("3: Atalho nao sobrescreve Etapa que ja saiu de nao-iniciado")]
        public async Task Atalho_NaoSobrescreveEtapaQueJaSaiu()
        {
            _fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(9, T0);

            (await Handler().ExecutarAsync(Entrada("fluxo", "production", por: "fulano", motivo: "urgente"), Ct)).IsSuccess.Should().BeTrue();

            Estado(EtapaDoFluxo.Qa).Should().Be(EstadoEtapa.Executando);
            _fluxo.Etapa(EtapaDoFluxo.Qa).PuladaPor.Should().BeNull();
            Estado(EtapaDoFluxo.Hml).Should().Be(EstadoEtapa.Pulada);
            _publicados.Cast<EntregaAtualizada>().Select(e => e.Etapa).Should().BeEquivalentTo(["hml", "prd"]);
        }

        [Theory, DisplayName("4: atalho_por acima de 200 ou atalho_motivo acima de 1000 recusa com pipeline.corpo-invalido sem gravar nada")]
        [InlineData(201, 5)]
        [InlineData(5, 1001)]
        public async Task Atalho_AcimaDoLimite_Recusa(int tamanhoPor, int tamanhoMotivo)
        {
            var r = await Handler().ExecutarAsync(
                Entrada("fluxo", "production", por: new string('a', tamanhoPor), motivo: new string('b', tamanhoMotivo)), Ct);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "pipeline.corpo-invalido" && e.Type == TipoErro.Failure);
            await _uow.DidNotReceive().SaveChangesAsync();
            await _ocupacoes.DidNotReceiveWithAnyArgs().ReservarAsync(default, default, default, default, default);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("4: exatamente 200 e 1000 caracteres passam")]
        public async Task Atalho_NoLimite_Passa()
        {
            var r = await Handler().ExecutarAsync(
                Entrada("fluxo", "production", por: new string('a', 200), motivo: new string('b', 1000)), Ct);

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("11: com Features:E1b desligada nao grava Etapa nem publica, e o limite do Atalho nao vale (comportamento antigo)")]
        public async Task FlagDesligada_ComportamentoAntigo()
        {
            _opcoes.E1bLigada.Returns(false);

            var r = await Handler().ExecutarAsync(
                Entrada("fluxo", "production", por: new string('a', 500), motivo: "m"), Ct);

            r.IsSuccess.Should().BeTrue();
            Estado(EtapaDoFluxo.Qa).Should().Be(EstadoEtapa.NaoIniciado);
            Estado(EtapaDoFluxo.Prd).Should().Be(EstadoEtapa.NaoIniciado);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("2: Etapa em estado que a tabela nao preve e no-op: o esteira-abrir segue aceito e nada e publicado")]
        public async Task TransicaoInvalida_NoOpSemRecusar()
        {
            _fluxo.RegistrarSha(Sha);
            var prd = _fluxo.Etapa(EtapaDoFluxo.Prd);
            prd.IniciarExecucao(1, T0);
            prd.AplicarStatusDaPipeline("success", T0.AddMinutes(1)).IsSuccess.Should().BeTrue();

            var r = await Handler().ExecutarAsync(Entrada("promocao", "production"), Ct);

            r.IsSuccess.Should().BeTrue();
            prd.Estado.Should().Be(EstadoEtapa.Concluido);
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("2: conflito de concorrencia na gravacao nao publica evento e nao vira erro")]
        public async Task ConflitoDeConcorrencia_NaoPublica()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns(Sha);

            var r = await Handler().ExecutarAsync(Entrada("fluxo", "qa"), Ct);

            r.IsSuccess.Should().BeTrue();
            await _eventos.DidNotReceiveWithAnyArgs().PublicarAsync(default!, default);
        }

        [Fact, DisplayName("10: conflito na gravacao da reserva de ocupacao recarrega a Etapa, reaplica o pipeline_id, grava e publica")]
        public async Task ConflitoNaReserva_ReaplicaEPublica()
        {
            _fluxo.RegistrarSha(Sha);
            var hml = _fluxo.Etapa(EtapaDoFluxo.Hml);
            var chamadas = 0;
            _ocupacoes.ReservarAsync(default, default, default, default, default).ReturnsForAnyArgs(_ =>
                ++chamadas == 1 ? throw new ConcorrenciaOtimistaException() : Task.FromResult(Result.Sucesso()));
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns(Sha);
            // O Reload real devolve a Etapa ao estado do banco (nao iniciada): simula-o.
            _entregas.RecarregarAsync(Arg.Any<EtapaFluxoEntity>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                typeof(EtapaFluxoEntity).GetProperty(nameof(EtapaFluxoEntity.Estado))!.SetValue(hml, EstadoEtapa.NaoIniciado);
                typeof(EtapaFluxoEntity).GetProperty(nameof(EtapaFluxoEntity.PipelineGitLabId))!.SetValue(hml, null);
                return Task.CompletedTask;
            });

            var r = await Handler().ExecutarAsync(Entrada("promocao", "hml"), Ct);

            r.IsSuccess.Should().BeTrue();
            await _ocupacoes.ReceivedWithAnyArgs(2).ReservarAsync(default, default, default, default, default);
            hml.Estado.Should().Be(EstadoEtapa.Executando);
            hml.PipelineGitLabId.Should().Be(555);
            _publicados.Should().ContainSingle().Which.Should().Be(
                new EntregaAtualizada(_appPublicId, _entrega.PublicId, _fluxo.PublicId, "hml", "executando"));
        }

        private sealed class Relogio : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0);
        }
    }
}
