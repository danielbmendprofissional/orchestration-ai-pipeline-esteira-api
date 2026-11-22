using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Consultas;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P1.7, critério 16: a rota de hotfixes (handler) lê do GitLab da Aplicação, com acesso por papel/MOR.
    [Trait("Fatia", "P1")]
    public class ObterHotfixesTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IPainelAplicacaoQuery _apps = Substitute.For<IPainelAplicacaoQuery>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();

        private static readonly AplicacaoNoPainel Com = new(
            Guid.NewGuid(), "Demo", "DEM", "demo", 10, "demo/demo-workspace",
            [new ProjetoNoPainel(RepositorioFatia.Api, 1, "demo/demo-api"), new ProjetoNoPainel(RepositorioFatia.Frontend, 2, "demo/demo-frontend")]);

        private static readonly AplicacaoNoPainel Sem = new(Guid.NewGuid(), "Sem", "SEM", "sem", null, null, []);

        public ObterHotfixesTests()
        {
            _usuario.Id.Returns("sub-1");
            _usuario.EhMor.Returns(false);
            _apps.ListarVisiveisAsync("sub-1", false, Arg.Any<CancellationToken>()).Returns([Com, Sem]);
            _gitlab.ListarIssuesAsync("demo", 10, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso([
                new IssueGitLabDto(51, FixturesFatias.Ler("titulo-issue-hotfix.txt"), "opened", ["ready-for-human"]),
                new IssueGitLabDto(52, "B2 — outra coisa", "opened", ["ready-for-human"])]));
            _gitlab.ListarMergeRequestsAsync("demo", 1, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([
                new MergeRequestGitLabDto(9, "Corrige juros", "opened", FixturesFatias.Ler("branch-hotfix.txt"), FixturesFatias.Ler("descricao-mr-hotfix.txt"), null)]));
            _gitlab.ListarMergeRequestsAsync("demo", 2, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([]));
        }

        private Task<Result<HotfixesDto>> Obter(Guid id)
            => new ObterHotfixesHandler(_apps, _gitlab, _usuario, new LinksDoGitLab("https://gitlab.exemplo.com")).Handle(new ObterHotfixesQuery(id), Ct);

        [Fact]
        public async Task Devolve_o_pedido_com_estado_mr_e_links_montados_de_ids()
        {
            var r = await Obter(Com.Id);

            r.IsSuccess.Should().BeTrue();
            var h = r.Value.Itens.Should().ContainSingle().Subject;
            h.Issue.Should().Be(51);
            h.Motivo.Should().Be("Arredondamento de juros errado no boleto");
            h.Estado.Should().Be("mr-aberta");
            h.LinkIssue.Should().Be("https://gitlab.exemplo.com/demo/demo-workspace/-/issues/51");
            h.Mr!.Repositorio.Should().Be("api");
            h.Mr.Iid.Should().Be(9);
            h.Mr.Branch.Should().Be("hotfix/juros-boleto");
            h.Mr.Link.Should().Be("https://gitlab.exemplo.com/demo/demo-api/-/merge_requests/9");
            r.Value.Aviso.Should().BeNull();
        }

        [Fact]
        public async Task Aplicacao_sem_workspace_devolve_aviso_sem_chamar_o_gitlab()
        {
            var r = await Obter(Sem.Id);

            r.IsSuccess.Should().BeTrue();
            r.Value.Itens.Should().BeEmpty();
            r.Value.Aviso!.Codigo.Should().Be("painel.sem-workspace");
            await _gitlab.DidNotReceiveWithAnyArgs().ListarIssuesAsync(default!, default, default);
        }

        [Fact]
        public async Task Sem_papel_a_aplicacao_nao_existe()
        {
            var r = await Obter(Guid.NewGuid());

            r.IsSuccess.Should().BeFalse();
            r.Errors[0].Code.Should().Be("aplicacao.nao-encontrada");
            r.Errors[0].Type.Should().Be(TipoErro.NotFound);
        }

        [Fact]
        public async Task Gitlab_fora_devolve_o_erro_sem_lancar()
        {
            _gitlab.ListarIssuesAsync("demo", 10, Arg.Any<CancellationToken>()).Returns(
                Result<IReadOnlyList<IssueGitLabDto>>.Falha(new Erro("gitlab.indisponivel", "Fora.", TipoErro.Failure)));

            var r = await Obter(Com.Id);

            r.IsSuccess.Should().BeFalse();
            r.Errors[0].Code.Should().Be("gitlab.indisponivel");
        }

        [Fact]
        public async Task Excecao_do_leitor_vira_erro_e_nao_sobe()
        {
            _gitlab.ListarIssuesAsync("demo", 10, Arg.Any<CancellationToken>()).Returns<Result<IReadOnlyList<IssueGitLabDto>>>(_ => throw new InvalidOperationException("x"));

            var r = await Obter(Com.Id);

            r.IsSuccess.Should().BeFalse();
            r.Errors[0].Code.Should().Be("gitlab.indisponivel");
        }
    }
}
