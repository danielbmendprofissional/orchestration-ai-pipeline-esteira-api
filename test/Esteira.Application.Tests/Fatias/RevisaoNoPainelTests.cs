using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.5, critérios 12, 14 e 16 no Painel e na gaveta: campos novos só com Features:P2; vazios com a flag desligada.
    [Trait("Fatia", "P2")]
    public class RevisaoNoPainelTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IPainelAplicacaoQuery _apps = Substitute.For<IPainelAplicacaoQuery>();
        private readonly IPainelDeFatias _painel = Substitute.For<IPainelDeFatias>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly IOpcoesDeRevisao _opcoes = Substitute.For<IOpcoesDeRevisao>();
        private readonly IAcoesDaFatiaQuery _acoes = Substitute.For<IAcoesDaFatiaQuery>();
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();

        private static readonly AplicacaoNoPainel App = new(
            Guid.NewGuid(), "Alfa", "ALF", "g/alfa", 10, "g/alfa/ws",
            [new ProjetoNoPainel(RepositorioFatia.Api, 1, "g/alfa/api", 100), new ProjetoNoPainel(RepositorioFatia.Frontend, 2, "g/alfa/front", 200)]);

        private static FatiaLida Fatia => new(
            "P2", "T", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x.md", 7,
            new ResultadoDeEstado(EstadoDaFatia.EmRevisao, null, false, false), false,
            new DetalheDaFatia(null, null, [], [], [new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — T", "opened", "feature/p2", "success")]));

        public RevisaoNoPainelTests()
        {
            _usuario.Id.Returns("sub-1");
            _apps.ListarVisiveisAsync("sub-1", false, Arg.Any<CancellationToken>()).Returns([App]);
            _painel.ObterAsync(Arg.Any<IReadOnlyList<AplicacaoDoPainel>>(), Arg.Any<CancellationToken>()).Returns(ci =>
                (IReadOnlyList<ResultadoDoPainel>)ci.Arg<IReadOnlyList<AplicacaoDoPainel>>()
                    .Select(e => new ResultadoDoPainel(e.Chave, SituacaoDoPainel.Ok, [Fatia], DateTimeOffset.UtcNow, false, null)).ToList());
            _painel.ObterAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>()).Returns(ci =>
                new ResultadoDoPainel(ci.Arg<AplicacaoDoPainel>().Chave, SituacaoDoPainel.Ok, [Fatia], DateTimeOffset.UtcNow, false, null));
            _opcoes.UrlModelo.Returns("https://{fatia}.preview.exemplo.com");
            _acesso.ResolverIdAsync(App.Id, Arg.Any<CancellationToken>()).Returns(42L);
            _autorizacao.ObterAcessoAsync(42L, Arg.Any<CancellationToken>()).Returns(new AcessoAplicacao(false, [PapelAplicacao.AprovadorTecnico]));
            _acoes.ListarAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, IReadOnlyList<AcaoNaFatia>>
            {
                [App.Id] =
                [
                    new AcaoNaFatia(Guid.NewGuid(), "P2", TipoAcaoBot.AbrirReview, EstadoAcaoBot.Confirmada, 100, RepositorioFatia.Api, 3, "Ana",
                        DateTime.UtcNow, DateTime.UtcNow, null, null),
                ],
            });
        }

        private RevisaoDoPainel Revisao(bool ligado)
        {
            _opcoes.Ligado.Returns(ligado);
            return new RevisaoDoPainel(_opcoes, _acoes, _acesso, _autorizacao);
        }

        private Task<Result<PainelListaDto>> Listar(RevisaoDoPainel? revisao)
            => new ListarFatiasHandler(_apps, _painel, _usuario, revisao)
                .Handle(new ListarFatiasQuery(new PaginacaoDto { TamanhoPagina = 100 }, "todas", null, null, null), Ct);

        private Task<Result<GavetaDto>> Gaveta(RevisaoDoPainel? revisao)
            => new ObterGavetaHandler(_apps, _painel, _usuario, new LinksDoGitLab("https://gitlab.exemplo.com"), revisao)
                .Handle(new ObterGavetaQuery(App.Id, "P2"), Ct);

        [Fact]
        public async Task Lista_com_flag_ligada_traz_acoes_review_e_decisao_da_linha()
        {
            var r = await Listar(Revisao(true));

            var linha = r.Value.Itens.Single();
            linha.Acoes!.PodeAprovar.Pode.Should().BeTrue();
            linha.Acoes.PodeAbrirReview.Should().Be(new PermissaoDaAcaoDto(false, "review-ja-pedido"));
            linha.Review.Should().NotBeNull();
            linha.Review!.Estado.Should().Be("aberto");
            linha.Decisao.Should().Be("nenhuma");
        }

        [Fact]
        public async Task Lista_com_flag_desligada_ou_sem_servico_vem_sem_os_campos_e_nao_consulta_acoes()
        {
            var desligada = await Listar(Revisao(false));
            var semServico = await Listar(null);

            foreach (var linha in desligada.Value.Itens.Concat(semServico.Value.Itens))
            {
                linha.Acoes.Should().BeNull();
                linha.Review.Should().BeNull();
                linha.Decisao.Should().BeNull();
            }
            await _acoes.DidNotReceiveWithAnyArgs().ListarAsync(default!, default);
        }

        [Fact]
        public async Task Gaveta_com_flag_ligada_lista_as_acoes_da_fatia()
        {
            var r = await Gaveta(Revisao(true));

            r.Value.Acoes!.PodeReprovar.Pode.Should().BeTrue();
            r.Value.AcoesPedidas.Should().ContainSingle().Which.Tipo.Should().Be("abrir-review");
            r.Value.Review!.Estado.Should().Be("aberto");
        }

        [Fact]
        public async Task Gaveta_com_flag_desligada_vem_com_campos_vazios()
        {
            var r = await Gaveta(Revisao(false));

            r.Value.Acoes.Should().BeNull();
            r.Value.Review.Should().BeNull();
            r.Value.Decisao.Should().BeNull();
            r.Value.AcoesPedidas.Should().BeEmpty();
        }
    }
}
