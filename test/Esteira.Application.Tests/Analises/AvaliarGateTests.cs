using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Analises;
using Esteira.Application.Analises.Gate;
using Esteira.Application.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Analises
{
    // E2.2 (spec 03-analises-e-gate.md, criterios 1 a 6): `esteira-gate` com portas substituidas.
    [Trait("Fatia", "E2")]
    public class AvaliarGateTests
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
        private readonly Guid _appPublicId = Guid.NewGuid();
        private readonly List<AnaliseEntity> _gravadas = [];
        private readonly List<EventoDeTempoReal> _publicados = [];
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public AvaliarGateTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);
            _fluxo.RegistrarSha(Sha);
            _fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(555, T0);

            _opcoes.E1bLigada.Returns(true);
            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(10L);
            _entregas.ObterPorPublicIdAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(_entrega);
            _acesso.ObterPublicIdAsync(7, Arg.Any<CancellationToken>()).Returns(_appPublicId);
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

        private AvaliarGateHandler Handler()
            => new(_porCaminho, _entregas, _projetos, _aplicacoes, _analises, _excecoes, _uow, new Relogio(), _acesso, _eventos, _opcoes);

        private sealed class Relogio : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0);
        }

        private static GateAchadoCorpo Achado(string severidade, string titulo = "SQL injection")
            => new(severidade, titulo, "csharp.sqli", "src/Repo.cs", 42, "descricao");

        private static GateAnaliseCorpo Analise(string fonte = "opengrep", string tipo = "ferramenta", string veredito = "aprovado",
            decimal? custo = null, string escopo = "artefato", params GateAchadoCorpo[] achados)
            => new(fonte, tipo, escopo, veredito, null, custo, achados);

        private GateCorpo Corpo(string process = "fluxo", string env = "qa", string? projeto = Caminho, string? sha = Sha, string? pipeline = "555",
            IReadOnlyList<GateAnaliseCorpo>? analises = null, Guid? fluxo = null)
            => new(process, env, _entrega.PublicId.ToString(), (fluxo ?? _fluxo.PublicId).ToString(), sha, projeto, pipeline, analises ?? [Analise()]);

        private static void Esperar(Result r, string codigo)
        {
            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
        }

        private void NadaGravado()
        {
            _analises.ReceivedCalls().Should().BeEmpty();
            _uow.ReceivedCalls().Should().BeEmpty();
        }

        [Fact, DisplayName("1: Achados dentro da Politica gravam Analises e decisao e respondem liberado com a contagem; media, baixa e info nao bloqueiam")]
        public async Task Liberado_GravaEResponde()
        {
            var corpo = Corpo(analises: [Analise(achados: [Achado("medium"), Achado("low"), Achado("INFO"), Achado("Medium", "Outro")]), Analise("agent-security", "agente", custo: 0.31m)]);

            var r = await Handler().ExecutarAsync(Caminho, corpo, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Decisao.Should().Be("liberado");
            r.Value.Contagem.Should().Be(new ContagemDeSeveridade(0, 0, 2, 1));
            _gravadas.Should().HaveCount(2);
            _gravadas[0].Achados.Should().HaveCount(4);
            _gravadas[1].CustoUsd.Should().Be(0.31m);
            _gravadas.Should().OnlyContain(a => a.PipelineGitLabId == 555 && a.Ambiente == AmbienteDeploy.Qa && a.Escopo == EscopoAnalise.Artefato);
            await _analises.Received(1).RegistrarDecisaoAsync(30, AmbienteDeploy.Qa, MomentoDoGate.Gate,
                Arg.Is<AvaliacaoDoGate>(a => a.Decisao == DecisaoGate.Liberado && a.Motivo == null), 555, T0, Arg.Any<CancellationToken>());
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Executando);
            _publicados.Should().BeEmpty();
        }

        [Fact, DisplayName("1: Modo avisa com excesso responde 200 avisou")]
        public async Task Avisa_Responde200()
        {
            _aplicacoes.ObterPoliticaAsync(7, AmbientePolitica.Qa, Arg.Any<CancellationToken>())
                .Returns(PoliticaSeveridadeEntity.Criar(7, AmbientePolitica.Qa, 0, 0, null, null, ModoPolitica.Avisa).Value);

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: [Achado("critical")])]), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Decisao.Should().Be("avisou");
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Executando);
        }

        [Fact, DisplayName("2: excesso com Modo bloqueia grava Analises e decisao, bloqueia a Etapa, publica o evento e recusa 409 gate.bloqueado com a contagem")]
        public async Task Bloqueado_GravaBloqueiaERecusa()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(veredito: "reprovado", achados: [Achado("high"), Achado("critical", "Outro")])]), Ct);

            Esperar(r, "gate.bloqueado");
            r.Errors[0].Type.Should().Be(TipoErro.Conflict);
            r.Errors[0].Message.Should().Contain("1").And.NotContain("SQL injection");
            _gravadas.Should().ContainSingle();
            await _analises.Received(1).RegistrarDecisaoAsync(30, AmbienteDeploy.Qa, MomentoDoGate.Gate,
                Arg.Is<AvaliacaoDoGate>(a => a.Decisao == DecisaoGate.Bloqueado && a.Motivo == "gate.bloqueado"), 555, T0, Arg.Any<CancellationToken>());
            var etapa = _fluxo.Etapa(EtapaDoFluxo.Qa);
            etapa.Estado.Should().Be(EstadoEtapa.Bloqueada);
            etapa.CodigoFalha.Should().Be("gate.bloqueado");
            await _uow.Received().SaveChangesAsync();
            _publicados.Should().ContainSingle().Which.Should().Be(new EntregaAtualizada(_appPublicId, _entrega.PublicId, _fluxo.PublicId, "qa", "bloqueada"));
        }

        [Fact, DisplayName("2: o Atalho (fluxo em production) nao pula o gate: a Etapa prd vai para bloqueada")]
        public async Task Atalho_NaoPulaOGate()
        {
            _fluxo.Etapa(EtapaDoFluxo.Prd).IniciarExecucao(555, T0);

            var r = await Handler().ExecutarAsync(Caminho, Corpo("fluxo", "production", analises: [Analise(achados: [Achado("high")])]), Ct);

            Esperar(r, "gate.bloqueado");
            _fluxo.Etapa(EtapaDoFluxo.Prd).Estado.Should().Be(EstadoEtapa.Bloqueada);
            _gravadas.Should().ContainSingle().Which.Ambiente.Should().Be(AmbienteDeploy.Producao);
        }

        [Fact, DisplayName("2: promocao em hml avalia a Politica de hml")]
        public async Task PromocaoHml_UsaPoliticaDeHml()
        {
            _fluxo.Etapa(EtapaDoFluxo.Hml).IniciarExecucao(555, T0);

            var r = await Handler().ExecutarAsync(Caminho, Corpo("promocao", "hml", analises: [Analise(achados: [Achado("high")])]), Ct);

            Esperar(r, "gate.bloqueado");
            _fluxo.Etapa(EtapaDoFluxo.Hml).Estado.Should().Be(EstadoEtapa.Bloqueada);
            await _aplicacoes.Received(1).ObterPoliticaAsync(7, AmbientePolitica.Hml, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("2: com Features:E1b desligado o gate recusa mas nao mexe na Etapa")]
        public async Task SemFlagE1b_NaoMexeNaEtapa()
        {
            _opcoes.E1bLigada.Returns(false);

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: [Achado("high")])]), Ct);

            Esperar(r, "gate.bloqueado");
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Executando);
            _publicados.Should().BeEmpty();
        }

        [Fact, DisplayName("3: Analise de ferramenta com veredito erro recusa 409 gate.analise-com-erro, grava e bloqueia a Etapa com esse motivo")]
        public async Task FerramentaComErro_Recusa()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise("trivy-fs", veredito: "erro")]), Ct);

            Esperar(r, "gate.analise-com-erro");
            r.Errors[0].Type.Should().Be(TipoErro.Conflict);
            _gravadas.Should().ContainSingle();
            _fluxo.Etapa(EtapaDoFluxo.Qa).CodigoFalha.Should().Be("gate.analise-com-erro");
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Bloqueada);
        }

        [Fact, DisplayName("3: Analise de agente com veredito erro so e registrada; decide pelas contagens")]
        public async Task AgenteComErro_SoRegistra()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise("agent-security", "agente", "erro")]), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Decisao.Should().Be("liberado");
            _gravadas.Should().ContainSingle().Which.Veredito.Should().Be(VereditoAnalise.Erro);
        }

        [Fact, DisplayName("4: sem Analises recusa 422 gate.sem-analises sem gravar nada")]
        public async Task SemAnalises_Recusa()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: []), Ct);

            Esperar(r, "gate.sem-analises");
            r.Errors[0].Type.Should().Be(TipoErro.Failure);
            NadaGravado();
        }

        public static IEnumerable<TheoryDataRow<string, GateAnaliseCorpo[]?>> CorposInvalidos()
        {
            var longo = new string('x', 301);
            yield return new("analises ausente", null);
            yield return new("severidade desconhecida", [Analise(achados: [Achado("grave")])]);
            yield return new("severidade ausente", [Analise(achados: [Achado(null!)])]);
            yield return new("fonte maiuscula", [Analise("OpenGrep")]);
            yield return new("fonte vazia", [Analise("")]);
            yield return new("fonte com 65", [Analise(new string('a', 65))]);
            yield return new("custo negativo", [Analise(custo: -0.01m)]);
            yield return new("escopo deploy no gate", [Analise(escopo: "deploy")]);
            yield return new("escopo invalido", [Analise(escopo: "xx")]);
            yield return new("tipo invalido", [Analise(tipo: "robo")]);
            yield return new("veredito invalido", [Analise(veredito: "talvez")]);
            yield return new("titulo vazio", [Analise(achados: [Achado("high", "")])]);
            yield return new("titulo longo", [Analise(achados: [Achado("high", longo)])]);
            yield return new("linha zero", [Analise(achados: [new GateAchadoCorpo("high", "t", null, null, 0, null)])]);
            yield return new("descricao longa", [Analise(achados: [new GateAchadoCorpo("high", "t", null, null, null, new string('d', 2001))])]);
            yield return new("21 analises", Enumerable.Range(0, 21).Select(i => Analise($"f{i}")).ToArray());
            yield return new("2001 achados", [Analise(achados: Enumerable.Range(0, 2001).Select(_ => Achado("low")).ToArray())]);
        }

        [Theory, DisplayName("4: corpo invalido recusa 422 pipeline.corpo-invalido sem gravar nada e sem repetir texto do Achado")]
        [MemberData(nameof(CorposInvalidos))]
        public async Task CorpoInvalido_Recusa(string caso, GateAnaliseCorpo[]? analises)
        {
            caso.Should().NotBeNull();
            var corpo = Corpo() with { Analises = analises };

            var r = await Handler().ExecutarAsync(Caminho, corpo, Ct);

            Esperar(r, "pipeline.corpo-invalido");
            r.Errors[0].Message.Should().NotContain("SQL injection");
            NadaGravado();
        }

        [Fact, DisplayName("4: exatamente 20 Analises e 2000 Achados sao aceitos")]
        public async Task NoLimite_Aceita()
        {
            var vinte = Enumerable.Range(0, 20).Select(i => Analise($"f{i}")).ToList();
            vinte[0] = Analise("f0", achados: Enumerable.Range(0, 2000).Select(_ => Achado("low")).ToArray());

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: vinte), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Contagem.Baixa.Should().Be(2000);
        }

        [Fact, DisplayName("5: projeto do corpo diferente do token recusa 403 uniforme sem consultar nada")]
        public async Task ProjetoDiferenteDoToken_403()
        {
            var r = await Handler().ExecutarAsync("grupo/outro", Corpo(), Ct);

            Esperar(r, "pipeline.projeto-nao-reconhecido");
            r.Errors[0].Type.Should().Be(TipoErro.Forbidden);
            await _porCaminho.DidNotReceiveWithAnyArgs().ObterIdAtivoAsync(default!, default);
            NadaGravado();
        }

        [Fact, DisplayName("5: Projeto nao cadastrado ou inativo recusa o mesmo 403")]
        public async Task ProjetoInativo_403()
        {
            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns((long?)null);

            Esperar(await Handler().ExecutarAsync(Caminho, Corpo(), Ct), "pipeline.projeto-nao-reconhecido");
            NadaGravado();
        }

        [Fact, DisplayName("5: sem Entrega aberta recusa 409 pipeline.sem-entrega-aberta")]
        public async Task SemEntrega_409()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(fluxo: Guid.NewGuid()), Ct);

            Esperar(r, "pipeline.sem-entrega-aberta");
            r.Errors[0].Type.Should().Be(TipoErro.Conflict);
            NadaGravado();
        }

        [Fact, DisplayName("5: SHA diferente do registrado recusa 409 pipeline.sha-diferente")]
        public async Task ShaDiferente_409()
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(sha: new string('a', 40)), Ct);

            Esperar(r, "pipeline.sha-diferente");
            _fluxo.Sha.Should().Be(Sha);
            NadaGravado();
        }

        [Fact, DisplayName("5: Fluxo sem Artefato recusa 409 pipeline.sem-artefato e nunca registra o SHA")]
        public async Task SemArtefato_409NaoRegistraSha()
        {
            var outro = EntregaEntity.Abrir(
                Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7), Id(PessoaEntity.Criar("sub-1", "F", "f@x.com", T0), 3),
                [new FluxoDaEntrega(Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10), true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value;
            Id(outro, 21);
            Id(outro.Fluxos[0], 31);
            _entregas.ObterPorPublicIdAsync(outro.PublicId, Arg.Any<CancellationToken>()).Returns(outro);
            var corpo = Corpo() with { EntregaId = outro.PublicId.ToString(), FluxoId = outro.Fluxos[0].PublicId.ToString() };

            var r = await Handler().ExecutarAsync(Caminho, corpo, Ct);

            Esperar(r, "pipeline.sem-artefato");
            outro.Fluxos[0].Sha.Should().BeNull();
            NadaGravado();
        }

        [Theory, DisplayName("5: process/env nao suportados recusam 422 pipeline.processo-nao-suportado")]
        [InlineData("fluxo", "hml")]
        [InlineData("promocao", "qa")]
        [InlineData("rollback", "production")]
        [InlineData("liberacao", "production")]
        public async Task ProcessoNaoSuportado_422(string process, string env)
        {
            var r = await Handler().ExecutarAsync(Caminho, Corpo(process, env), Ct);

            Esperar(r, "pipeline.processo-nao-suportado");
            NadaGravado();
        }

        [Fact, DisplayName("5: Ambiente desabilitado recusa 422 ambiente.desabilitado")]
        public async Task AmbienteDesabilitado_422()
        {
            _projetos.ObterAmbienteAsync(10, AmbienteDeploy.Qa, Arg.Any<CancellationToken>()).Returns(AmbienteProjetoEntity.Criar(10, AmbienteDeploy.Qa, false).Value);

            Esperar(await Handler().ExecutarAsync(Caminho, Corpo(), Ct), "ambiente.desabilitado");
            NadaGravado();
        }

        [Fact, DisplayName("6: conflito de unicidade na gravacao (retry simultaneo do mesmo job) descarta o pendente e regrava, sem 500")]
        public async Task ViolacaoDeUnicidade_Regrava()
        {
            _uow.SaveChangesAsync().Returns(
                _ => Task.FromException<int>(new ViolacaoDeUnicidadeException("IX_Analise")),
                _ => Task.FromResult(1));

            var r = await Handler().ExecutarAsync(Caminho, Corpo(), Ct);

            r.IsSuccess.Should().BeTrue();
            _analises.Received(1).DescartarPendentes();
            await _analises.Received(2).SubstituirAsync(30, AmbienteDeploy.Qa, EscopoAnalise.Artefato, 555, Arg.Any<IReadOnlyList<AnaliseEntity>>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("6: conflito persistente de gravacao vira 409 pipeline.conflito-de-gravacao, nunca excecao")]
        public async Task ConflitoPersistente_409()
        {
            _uow.SaveChangesAsync().Returns(_ => Task.FromException<int>(new ViolacaoDeUnicidadeException("IX_Analise")));

            var r = await Handler().ExecutarAsync(Caminho, Corpo(), Ct);

            Esperar(r, "pipeline.conflito-de-gravacao");
            r.Errors[0].Type.Should().Be(TipoErro.Conflict);
        }

        [Fact, DisplayName("6: conflito de concorrencia na Etapa (webhook) relê a Etapa e reaplica o bloqueio")]
        public async Task ConcorrenciaNaEtapa_ReleEReaplica()
        {
            _uow.SaveChangesAsync().Returns(
                _ => Task.FromException<int>(new ConcorrenciaOtimistaException()),
                _ => Task.FromResult(1));

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: [Achado("high")])]), Ct);

            Esperar(r, "gate.bloqueado");
            await _entregas.Received(1).RecarregarAsync(_fluxo.Etapa(EtapaDoFluxo.Qa), Arg.Any<CancellationToken>());
            _fluxo.Etapa(EtapaDoFluxo.Qa).Estado.Should().Be(EstadoEtapa.Bloqueada);
        }

        [Fact, DisplayName("2: falha ao publicar entrega.atualizada nunca derruba o gate")]
        public async Task FalhaNoPublicador_NaoDerruba()
        {
            _eventos.PublicarAsync(Arg.Any<EventoDeTempoReal>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new InvalidOperationException("sse")));

            var r = await Handler().ExecutarAsync(Caminho, Corpo(analises: [Analise(achados: [Achado("high")])]), Ct);

            Esperar(r, "gate.bloqueado");
        }
    }
}
