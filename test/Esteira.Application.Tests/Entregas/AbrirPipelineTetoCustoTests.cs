using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
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
    // X3.3, critérios 8 e 9: o `esteira-abrir` devolve `teto_custo_usd` (efetivo) só quando responde 200.
    [Trait("Fatia", "X3")]
    public class AbrirPipelineTetoCustoTests
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
        private readonly ITetoCustoDaAplicacaoQuery _teto = Substitute.For<ITetoCustoDaAplicacaoQuery>();
        private readonly IOpcoesDosAgentes _opcoes = Substitute.For<IOpcoesDosAgentes>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly EntregaEntity _entrega;
        private readonly FluxoEntity _fluxo;

        public AbrirPipelineTetoCustoTests()
        {
            var aplicacao = Id(AplicacaoEntity.Criar("App", "APP", 1, "g").Value, 7);
            var pessoa = Id(PessoaEntity.Criar("sub-1", "Fulano", "f@x.com", T0), 3);
            var projeto = Id(ProjetoEntity.Criar(7, 101, Caminho, "api", Stack.Dotnet), 10);
            _entrega = Id(EntregaEntity.Abrir(aplicacao, pessoa,
                [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, [new FatiaDaEntrega("B3", true)])], T0).Value, 20);
            _fluxo = Id(_entrega.Fluxos[0], 30);

            _porCaminho.ObterIdAtivoAsync(Caminho, Arg.Any<CancellationToken>()).Returns(10L);
            _entregas.ObterPorPublicIdAsync(_entrega.PublicId, Arg.Any<CancellationToken>()).Returns(_entrega);
            foreach (var a in new[] { AmbienteDeploy.Qa, AmbienteDeploy.Hml, AmbienteDeploy.Producao })
                _projetos.ObterAmbienteAsync(10, a, Arg.Any<CancellationToken>()).Returns(AmbienteProjetoEntity.Criar(10, a, true).Value);
            _ocupacoes.ReservarAsync(default, default, default, default, default).ReturnsForAnyArgs(Result.Sucesso());
            _destinos.ListarAsync(default, default, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<DestinoNaResposta>>([]));
            _opcoes.TetoCustoPadraoUsd.Returns(2.00m);
        }

        private static T Id<T>(T entidade, long id) where T : EntityBase
        {
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(entidade, id);
            return entidade;
        }

        private AbrirPipelineHandler Handler() => new(_porCaminho, _entregas, _projetos, _ocupacoes, _destinos, _uow, TimeProvider.System, _teto, _opcoes);

        private AbrirPipelineEntrada Entrada(string process, string env)
        {
            var atalho = process == "fluxo" && env == "production";
            return new(process, env, _entrega.PublicId, _fluxo.PublicId, Sha, Caminho, "1", null,
                atalho ? "fulano" : null, atalho ? "urgente" : null, false);
        }

        // A promoção exige o SHA já registrado no Fluxo.
        private void ShaRegistrado() => _fluxo.RegistrarSha(Sha);

        [Theory, DisplayName("8: 200 traz o teto definido da Aplicação em cada combinação process/env")]
        [InlineData("fluxo", "qa")]
        [InlineData("fluxo", "production")]
        [InlineData("promocao", "hml")]
        [InlineData("promocao", "production")]
        public async Task Sucesso_TetoDaAplicacao(string process, string env)
        {
            ShaRegistrado();
            _teto.ObterAsync(7, Arg.Any<CancellationToken>()).Returns(3.50m);

            var r = await Handler().ExecutarAsync(Entrada(process, env), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.TetoCustoUsd.Should().Be(3.50m);
        }

        [Theory, DisplayName("8: Aplicação sem teto definido devolve o padrão da Esteira")]
        [InlineData("fluxo", "qa")]
        [InlineData("promocao", "hml")]
        public async Task Sucesso_SemTeto_UsaPadrao(string process, string env)
        {
            ShaRegistrado();
            _teto.ObterAsync(7, Arg.Any<CancellationToken>()).Returns((decimal?)null);
            _opcoes.TetoCustoPadraoUsd.Returns(4.25m);

            var r = await Handler().ExecutarAsync(Entrada(process, env), Ct);

            r.Value.TetoCustoUsd.Should().Be(4.25m);
        }

        [Fact, DisplayName("9: recusa (4xx) não traz teto e nem consulta a porta")]
        public async Task Recusa_SemTeto()
        {
            var entrada = Entrada("fluxo", "qa") with { EntregaId = Guid.NewGuid() };

            var r = await Handler().ExecutarAsync(entrada, Ct);

            r.IsSuccess.Should().BeFalse();
            await _teto.DidNotReceiveWithAnyArgs().ObterAsync(default, default);
        }
    }
}
