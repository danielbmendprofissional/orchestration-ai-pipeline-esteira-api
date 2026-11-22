using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // P1.6, critério 12: o instantâneo carrega o que a gaveta precisa (filhas, tasks, MR por repositório, branch).
    [Trait("Fatia", "P1")]
    public class DetalheDaFatiaTests
    {
        private static readonly string Mapa = FixturesFatias.Ler("mapa-de-fatias.md");

        private static FatiaLida P1(IssueGitLabDto[] issues, Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>? mrs = null)
            => MontadorDeFatias.Montar(
                    Mapa, issues, mrs ?? new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>(),
                    new Dictionary<string, string>(), new Dictionary<long, string> { [37] = FixturesFatias.Ler("bloco-execucao-b2.md").Replace("B2", "P1") })
                .Single(f => f.Codigo == "P1");

        [Fact]
        public void Carrega_mae_filhas_e_tasks_com_sha_e_executor()
        {
            var fatia = P1([
                new IssueGitLabDto(37, "P1 — Painel de Fatias", "opened", ["em-execucao"]),
                new IssueGitLabDto(4, "P1.1 — migration", "closed", ["backend"]),
                new IssueGitLabDto(5, "P1.2 — rota", "closed", ["frontend"]),
                new IssueGitLabDto(99, "Z9.1 — de outra fatia", "opened", [])]);

            fatia.Detalhe.Should().NotBeNull();
            fatia.Detalhe!.MaeTitulo.Should().Be("P1 — Painel de Fatias");
            fatia.Detalhe.Filhas.Select(f => (f.Iid, f.NumeroTask, f.Repositorio)).Should().Equal(
                (4L, 1, (RepositorioFatia?)RepositorioFatia.Api), (5L, 2, (RepositorioFatia?)RepositorioFatia.Frontend));
            fatia.Detalhe.Tasks.Select(t => (t.NumeroTask, t.IssueFilha, t.Sha, t.EmAndamento, t.Repositorio)).Should().Equal(
                (1, 4L, (string?)"0b4bd79", false, (RepositorioFatia?)RepositorioFatia.Api),
                (2, 5L, "3fa91c2", false, RepositorioFatia.Frontend),
                (3, 6L, null, true, null));
            fatia.Detalhe.Tasks[1].Executor.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Carrega_a_mr_de_cada_repositorio_com_a_branch_de_origem()
        {
            var mrs = new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>
            {
                [RepositorioFatia.Api] = [
                    new MergeRequestGitLabDto(5, "P1 — Painel", "merged", "feature/p1-painel", null, null),
                    new MergeRequestGitLabDto(8, "Z9 — outra", "opened", "feature/z9-outra", null, null)],
                [RepositorioFatia.Frontend] = [new MergeRequestGitLabDto(6, "P1 — Painel", "opened", "feature/p1-painel", null, "success")],
            };

            var fatia = P1([new IssueGitLabDto(37, "P1 — Painel de Fatias", "opened", ["em-execucao"])], mrs);

            fatia.Detalhe!.Mrs.Select(m => (m.Repositorio, m.Iid, m.Estado, m.Branch)).Should().Equal(
                (RepositorioFatia.Api, 5L, "merged", "feature/p1-painel"),
                (RepositorioFatia.Frontend, 6L, "opened", "feature/p1-painel"));
        }

        [Fact]
        public void Fatia_planejada_nao_tem_detalhe_vazio_que_quebre()
        {
            var planejada = MontadorDeFatias.Montar(Mapa, [], new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>(),
                new Dictionary<string, string>(), new Dictionary<long, string>()).First();

            planejada.Detalhe.Should().NotBeNull();
            planejada.Detalhe!.Filhas.Should().BeEmpty();
            planejada.Detalhe.Tasks.Should().BeEmpty();
            planejada.Detalhe.Mrs.Should().BeEmpty();
        }
    }
}
