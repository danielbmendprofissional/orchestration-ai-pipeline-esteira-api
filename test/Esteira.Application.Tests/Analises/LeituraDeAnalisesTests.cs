using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Analises.Queries;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Entregas.Queries.Obter;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using NSubstitute;

namespace Esteira.Application.Tests.Analises
{
    // E2.4 (spec 03-analises-e-gate.md, critérios 12 e 13): leitura das Análises da Etapa e dos Achados, 404 uniforme.
    [Trait("Fatia", "E2")]
    public class LeituraDeAnalisesTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAnaliseLeituraQuery _query = Substitute.For<IAnaliseLeituraQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly Guid _entrega = Guid.NewGuid(), _fluxo = Guid.NewGuid(), _analise = Guid.NewGuid();

        public LeituraDeAnalisesTests()
        {
            _query.LocalizarFluxoAsync(_entrega, _fluxo, Arg.Any<CancellationToken>()).Returns(new FluxoLocalizado(7, 30));
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, [PapelAplicacao.Membro]));
        }

        private ObterAnalisesDaEtapaHandler Etapa() => new(_query, _autorizacao);
        private ListarAchadosHandler Achados() => new(_query, _autorizacao);

        [Theory, DisplayName("A etapa qa/hml/prd mapeia para o Ambiente qa/hml/producao")]
        [InlineData("qa", AmbienteDeploy.Qa)]
        [InlineData("hml", AmbienteDeploy.Hml)]
        [InlineData("prd", AmbienteDeploy.Producao)]
        public async Task MapeiaEtapa(string etapa, AmbienteDeploy esperado)
        {
            var dto = new AnalisesDaEtapaDto(null, []);
            _query.AnalisesDaEtapaAsync(30, esperado, Arg.Any<CancellationToken>()).Returns(dto);

            var r = await Etapa().Handle(new ObterAnalisesDaEtapaQuery(_entrega, _fluxo, etapa), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeSameAs(dto);
            r.Value.Gate.Should().BeNull();
        }

        [Fact, DisplayName("Etapa desconhecida responde o mesmo 404 da Entrega inexistente")]
        public async Task EtapaDesconhecida()
        {
            var r = await Etapa().Handle(new ObterAnalisesDaEtapaQuery(_entrega, _fluxo, "abertura"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
        }

        [Fact, DisplayName("Fluxo inexistente ou fora da Entrega responde entrega.nao-encontrada")]
        public async Task FluxoInexistente()
        {
            var r = await Etapa().Handle(new ObterAnalisesDaEtapaQuery(_entrega, Guid.NewGuid(), "qa"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
        }

        [Fact, DisplayName("Sem papel na Aplicação (nem MOR) responde o mesmo 404 e não consulta as Análises")]
        public async Task SemAcesso()
        {
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, []));

            var r = await Etapa().Handle(new ObterAnalisesDaEtapaQuery(_entrega, _fluxo, "qa"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
            await _query.DidNotReceiveWithAnyArgs().AnalisesDaEtapaAsync(default, default, default);
        }

        [Fact, DisplayName("MOR sem papel enxerga as Análises")]
        public async Task Mor()
        {
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(true, []));
            _query.AnalisesDaEtapaAsync(30, AmbienteDeploy.Qa, Arg.Any<CancellationToken>()).Returns(new AnalisesDaEtapaDto(null, []));

            var r = await Etapa().Handle(new ObterAnalisesDaEtapaQuery(_entrega, _fluxo, "qa"), Ct);

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Achados: Análise de outro Fluxo ou inexistente responde o mesmo 404")]
        public async Task AnaliseInexistente()
        {
            _query.LocalizarAnaliseAsync(30, _analise, Arg.Any<CancellationToken>()).Returns((long?)null);

            var r = await Achados().Handle(new ListarAchadosQuery(_entrega, _fluxo, _analise, null, new PaginacaoDto()), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
        }

        [Fact, DisplayName("Achados: sem acesso responde o mesmo 404 sem localizar a Análise")]
        public async Task AchadosSemAcesso()
        {
            _autorizacao.ObterAcessoAsync(7, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, []));

            var r = await Achados().Handle(new ListarAchadosQuery(_entrega, _fluxo, _analise, null, new PaginacaoDto()), Ct);

            r.Errors.Should().ContainSingle().Which.Should().BeSameAs(EntregaErros.NaoEncontrada);
            await _query.DidNotReceiveWithAnyArgs().LocalizarAnaliseAsync(default, default, default);
        }

        [Fact, DisplayName("Achados: severidade desconhecida no filtro é erro de validação (nada filtrado em silêncio)")]
        public async Task SeveridadeInvalida()
        {
            var r = await Achados().Handle(new ListarAchadosQuery(_entrega, _fluxo, _analise, "gravissima", new PaginacaoDto()), Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("analise.severidade-invalida");
        }

        [Fact, DisplayName("Achados: repassa a Análise, o filtro de severidade e a paginação ao query repository")]
        public async Task Repassa()
        {
            var pag = new PaginacaoDto { Pagina = 2, TamanhoPagina = 5 };
            var pagina = new PaginaDto<AchadoDto> { Itens = [], Pagina = 2, TamanhoPagina = 5, Total = 0 };
            _query.LocalizarAnaliseAsync(30, _analise, Arg.Any<CancellationToken>()).Returns(99L);
            _query.ListarAchadosAsync(99, SeveridadeAchado.Alta, pag, Arg.Any<CancellationToken>()).Returns(pagina);

            var r = await Achados().Handle(new ListarAchadosQuery(_entrega, _fluxo, _analise, "ALTA", pag), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeSameAs(pagina);
        }
    }
}
