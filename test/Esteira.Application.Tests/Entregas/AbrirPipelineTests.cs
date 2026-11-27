using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using NSubstitute;

namespace Esteira.Application.Tests.Entregas
{
    // E1.4 (spec 01-entrega-e-fluxo.md, critérios 4, 7, 8, 9, 10 e 15): passos 3 a 9 do `esteira-abrir`, portas substituídas.
    [Trait("Fatia", "E1")]
    public class AbrirPipelineTests
    {
        private const string Caminho = "grupo/api";
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";
        private const string OutroSha = "ffffffffffffffffffffffffffffffffffffffff";
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IProjetoPorCaminhoQuery _porCaminho = Substitute.For<IProjetoPorCaminhoQuery>();
        private readonly IEntregaRepositorio _entregas = Substitute.For<IEntregaRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IOcupacaoAmbienteRepositorio _ocupacoes = Substitute.For<IOcupacaoAmbienteRepositorio>();
        private readonly IDestinosDaPipelineQuery _destinos = Substitute.For<IDestinosDaPipelineQuery>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly ProjetoEntity _projeto;
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public AbrirPipelineTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            _projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(_projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true), new FatiaDaEntrega("B2", true), new FatiaDaEntrega("B4", false)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);

            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(10L);
            _entregas.ObterPorPublicIdAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(_entrega);
            foreach (var a in new[] { AmbienteDeploy.Qa, AmbienteDeploy.Hml, AmbienteDeploy.Producao })
                Ambiente(a, true);
            _ocupacoes.ReservarAsync(default, default, default, default, default).ReturnsForAnyArgs(Result.Sucesso());
            _destinos.ListarAsync(default, default, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<DestinoNaResposta>>([]));
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private void Ambiente(AmbienteDeploy a, bool habilitado)
            => _projetos.ObterAmbienteAsync(10, a, Arg.Any<CancellationToken>())
                .Returns(AmbienteProjetoEntity.Criar(10, a, habilitado || a == AmbienteDeploy.Producao).Value);

        private void AmbienteSemCadastro(AmbienteDeploy a)
            => _projetos.ObterAmbienteAsync(10, a, Arg.Any<CancellationToken>()).Returns((AmbienteProjetoEntity?)null);

        private AbrirPipelineHandler Handler() => new(_porCaminho, _entregas, _projetos, _ocupacoes, _destinos, _uow, new Relogio());

        private AbrirPipelineEntrada Entrada(string process = "fluxo", string env = "qa", string sha = Sha, Guid? entrega = null,
            Guid? fluxo = null, string? atalhoPor = null, string? atalhoMotivo = null)
            => new(process, env, entrega ?? _entrega.PublicId, fluxo ?? _fluxo.PublicId, sha, Caminho, "1", null, atalhoPor, atalhoMotivo, false);

        private static void Recusou(Result<AbrirPipelineResposta> r, string codigo, TipoErro tipo)
        {
            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == codigo && e.Type == tipo);
        }

        private async Task NadaGravado()
        {
            await _uow.DidNotReceive().SaveChangesAsync();
            await _ocupacoes.DidNotReceiveWithAnyArgs().ReservarAsync(default, default, default, default, default);
        }

        [Fact, DisplayName("4: fluxo em qa registra o SHA no Fluxo, grava e responde com as Fatias com flag =true e os destinos")]
        public async Task FluxoQa_RegistraShaEResponde()
        {
            var destinos = new List<DestinoNaResposta> { new("local", 1, new Dictionary<string, string> { ["K8S_NAMESPACE"] = "qa" }) };
            _destinos.ListarAsync(10, AmbienteDeploy.Qa, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DestinoNaResposta>>(destinos));

            var r = await Handler().ExecutarAsync(Entrada(), Ct);

            r.IsSuccess.Should().BeTrue();
            _fluxo.Sha.Should().Be(Sha);
            r.Value.FeatureFlags.Should().Be("B2=true,B3=true");
            r.Value.Destinos.Should().BeEquivalentTo(destinos);
            await _uow.Received(1).SaveChangesAsync();
            await _ocupacoes.DidNotReceiveWithAnyArgs().ReservarAsync(default, default, default, default, default);
        }

        [Fact, DisplayName("4: corrida no fluxo (concorrência otimista) com SHA diferente do vencedor responde 409 pipeline.sha-diferente")]
        public async Task FluxoCorrida_ShaDiferenteDoVencedor_Recusa()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns("ffffffffffffffffffffffffffffffffffffffff");

            var r = await Handler().ExecutarAsync(Entrada(), Ct);

            Recusou(r, "pipeline.sha-diferente", TipoErro.Conflict);
        }

        [Fact, DisplayName("4: corrida no fluxo em que o vencedor gravou o mesmo SHA segue normal (200)")]
        public async Task FluxoCorrida_MesmoShaDoVencedor_Segue()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns(Sha);

            var r = await Handler().ExecutarAsync(Entrada(), Ct);

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("4: corrida na reserva de production (concorrência no Fluxo) com SHA diferente do vencedor responde 409 pipeline.sha-diferente")]
        public async Task FluxoProducao_CorridaNaReserva_ShaDiferenteDoVencedor_Recusa()
        {
            _ocupacoes.ReservarAsync(default, default, default, default, default)
                .ReturnsForAnyArgs<Task<Result>>(_ => throw new ConcorrenciaOtimistaException());
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns(OutroSha);

            var r = await Handler().ExecutarAsync(Entrada(env: "production", atalhoPor: "fulano", atalhoMotivo: "urgente"), Ct);

            Recusou(r, "pipeline.sha-diferente", TipoErro.Conflict);
        }

        [Fact, DisplayName("4: corrida na reserva de production em que o vencedor gravou o mesmo SHA reserva de novo e segue (200)")]
        public async Task FluxoProducao_CorridaNaReserva_MesmoShaDoVencedor_ReservaESegue()
        {
            _ocupacoes.ReservarAsync(default, default, default, default, default)
                .ReturnsForAnyArgs<Task<Result>>(_ => throw new ConcorrenciaOtimistaException(), _ => Task.FromResult(Result.Sucesso()));
            _entregas.ObterShaGravadoDoFluxoAsync(30, Arg.Any<CancellationToken>()).Returns(Sha);

            var r = await Handler().ExecutarAsync(Entrada(env: "production", atalhoPor: "fulano", atalhoMotivo: "urgente"), Ct);

            r.IsSuccess.Should().BeTrue();
            await _ocupacoes.Received(2).ReservarAsync(10, AmbienteDeploy.Producao, 30, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("4: o mesmo SHA de novo no fluxo (retry de job) responde 200")]
        public async Task FluxoQa_ShaRepetido_Idempotente()
        {
            (await Handler().ExecutarAsync(Entrada(), Ct)).IsSuccess.Should().BeTrue();
            (await Handler().ExecutarAsync(Entrada(), Ct)).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("7: Fluxo inexistente, de outra Entrega ou de outro Projeto responde 409 pipeline.sem-entrega-aberta e não grava")]
        public async Task SemEntregaAberta_FluxoOuEntregaOuProjetoErrados()
        {
            Recusou(await Handler().ExecutarAsync(Entrada(fluxo: Guid.NewGuid()), Ct), "pipeline.sem-entrega-aberta", TipoErro.Conflict);
            Recusou(await Handler().ExecutarAsync(Entrada(entrega: Guid.NewGuid()), Ct), "pipeline.sem-entrega-aberta", TipoErro.Conflict);

            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(99L);
            Recusou(await Handler().ExecutarAsync(Entrada(), Ct), "pipeline.sem-entrega-aberta", TipoErro.Conflict);

            _fluxo.Sha.Should().BeNull();
            await NadaGravado();
        }

        [Fact, DisplayName("7: Entrega encerrada responde 409 pipeline.sem-entrega-aberta")]
        public async Task EntregaEncerrada()
        {
            typeof(EntregaEntity).GetProperty(nameof(EntregaEntity.Situacao))!.SetValue(_entrega, SituacaoEntrega.Encerrada);

            Recusou(await Handler().ExecutarAsync(Entrada(), Ct), "pipeline.sem-entrega-aberta", TipoErro.Conflict);
            await NadaGravado();
        }

        [Theory, DisplayName("8: liberacao, flag, rollback e rotulo respondem 422 pipeline.processo-nao-suportado, antes de qualquer consulta")]
        [InlineData("liberacao", "production")]
        [InlineData("flag", "qa")]
        [InlineData("rollback", "production")]
        [InlineData("rotulo", "")]
        [InlineData("promocao", "qa")]
        [InlineData("fluxo", "hml")]
        public async Task ProcessoNaoSuportado(string process, string env)
        {
            Recusou(await Handler().ExecutarAsync(Entrada(process, env), Ct), "pipeline.processo-nao-suportado", TipoErro.Failure);

            await _entregas.DidNotReceiveWithAnyArgs().ObterPorPublicIdAsync(default, default);
            await NadaGravado();
        }

        [Theory, DisplayName("15: Ambiente desabilitado ou sem cadastro responde 422 ambiente.desabilitado e não grava")]
        [InlineData("fluxo", "qa", false)]
        [InlineData("promocao", "hml", false)]
        [InlineData("promocao", "hml", true)]
        public async Task AmbienteDesabilitado(string process, string env, bool semCadastro)
        {
            var ambiente = env == "qa" ? AmbienteDeploy.Qa : AmbienteDeploy.Hml;
            if (semCadastro) AmbienteSemCadastro(ambiente); else Ambiente(ambiente, false);

            Recusou(await Handler().ExecutarAsync(Entrada(process, env), Ct), "ambiente.desabilitado", TipoErro.Failure);
            await NadaGravado();
        }

        [Fact, DisplayName("9: fluxo com SHA diferente do registrado responde 409 pipeline.sha-diferente e mantém o SHA")]
        public async Task FluxoShaDiferente()
        {
            (await Handler().ExecutarAsync(Entrada(), Ct)).IsSuccess.Should().BeTrue();
            _uow.ClearReceivedCalls();

            Recusou(await Handler().ExecutarAsync(Entrada(sha: OutroSha), Ct), "pipeline.sha-diferente", TipoErro.Conflict);
            _fluxo.Sha.Should().Be(Sha);
            await NadaGravado();
        }

        [Fact, DisplayName("9: promocao de Fluxo sem SHA responde 409 pipeline.sem-artefato")]
        public async Task PromocaoSemArtefato()
        {
            Recusou(await Handler().ExecutarAsync(Entrada("promocao", "hml"), Ct), "pipeline.sem-artefato", TipoErro.Conflict);
            await NadaGravado();
        }

        [Fact, DisplayName("9: promocao com SHA diferente do Fluxo responde 409 pipeline.sha-diferente")]
        public async Task PromocaoShaDiferente()
        {
            _fluxo.RegistrarSha(Sha);

            Recusou(await Handler().ExecutarAsync(Entrada("promocao", "hml", OutroSha), Ct), "pipeline.sha-diferente", TipoErro.Conflict);
            await NadaGravado();
        }

        [Theory, DisplayName("10: fluxo em production sem atalho_por ou atalho_motivo (vazio ou em branco) responde 422 pipeline.atalho-obrigatorio")]
        [InlineData(null, "motivo")]
        [InlineData("", "motivo")]
        [InlineData("fulano", null)]
        [InlineData("fulano", "  ")]
        [InlineData(null, null)]
        public async Task AtalhoObrigatorio(string? por, string? motivo)
        {
            Recusou(await Handler().ExecutarAsync(Entrada("fluxo", "production", atalhoPor: por, atalhoMotivo: motivo), Ct), "pipeline.atalho-obrigatorio", TipoErro.Failure);
            await NadaGravado();
        }

        [Fact, DisplayName("10: fluxo em production com Atalho completo registra o SHA, reserva produção e responde as flags desligadas")]
        public async Task FluxoProducaoComAtalho()
        {
            var r = await Handler().ExecutarAsync(Entrada("fluxo", "production", atalhoPor: "fulano", atalhoMotivo: "incidente"), Ct);

            r.IsSuccess.Should().BeTrue();
            _fluxo.Sha.Should().Be(Sha);
            r.Value.FeatureFlags.Should().Be("B2=false,B3=false");
            await _ocupacoes.Received(1).ReservarAsync(10, AmbienteDeploy.Producao, 30, T0, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("10: promocao com Atalho preenchido não o honra: o resultado é o mesmo de uma promocao sem Atalho")]
        public async Task PromocaoNaoHonraAtalho()
        {
            _fluxo.RegistrarSha(Sha);

            var comAtalho = await Handler().ExecutarAsync(Entrada("promocao", "production", atalhoPor: "fulano", atalhoMotivo: "x"), Ct);
            var semAtalho = await Handler().ExecutarAsync(Entrada("promocao", "production"), Ct);

            comAtalho.IsSuccess.Should().BeTrue();
            semAtalho.IsSuccess.Should().BeTrue();
            comAtalho.Value.Should().BeEquivalentTo(semAtalho.Value);
        }

        [Fact, DisplayName("8: promocao em hml reserva o Ambiente e responde as flags =true")]
        public async Task PromocaoHml_Reserva()
        {
            _fluxo.RegistrarSha(Sha);

            var r = await Handler().ExecutarAsync(Entrada("promocao", "hml"), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.FeatureFlags.Should().Be("B2=true,B3=true");
            await _ocupacoes.Received(1).ReservarAsync(10, AmbienteDeploy.Hml, 30, T0, Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("8: Ambiente ocupado por outro Fluxo responde 409 ambiente.ocupado e não grava")]
        public async Task Ocupado()
        {
            _fluxo.RegistrarSha(Sha);
            _ocupacoes.ReservarAsync(default, default, default, default, default)
                .ReturnsForAnyArgs(Result.Falha(OcupacaoAmbienteEntity.Ocupado()));

            var r = await Handler().ExecutarAsync(Entrada("promocao", "hml"), Ct);

            Recusou(r, "ambiente.ocupado", TipoErro.Conflict);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("15: destinos do Ambiente por Ordem, com configuração, vêm na resposta; habilitado sem Destino devolve lista vazia")]
        public async Task Destinos()
        {
            var lista = new List<DestinoNaResposta>
            {
                new("a", 1, new Dictionary<string, string> { ["K"] = "1" }),
                new("b", 2, new Dictionary<string, string>())
            };
            _destinos.ListarAsync(10, AmbienteDeploy.Qa, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DestinoNaResposta>>(lista));

            (await Handler().ExecutarAsync(Entrada(), Ct)).Value.Destinos.Select(d => d.Nome).Should().Equal("a", "b");

            _destinos.ListarAsync(10, AmbienteDeploy.Qa, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DestinoNaResposta>>([]));
            (await Handler().ExecutarAsync(Entrada(), Ct)).Value.Destinos.Should().BeEmpty();
        }

        private sealed class Relogio : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0);
        }
    }
}
