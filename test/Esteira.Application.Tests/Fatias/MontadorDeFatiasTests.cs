using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // P1.4, critérios 4 e 5: uma Fatia por linha do mapa, na ordem, com risco e estado; formato antigo nunca derruba a lista.
    [Trait("Fatia", "P1")]
    public class MontadorDeFatiasTests
    {
        private static readonly string Mapa = FixturesFatias.Ler("mapa-de-fatias.md");

        private static IReadOnlyDictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>> SemMrs() =>
            new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>();

        [Fact]
        public void Devolve_uma_fatia_por_linha_do_mapa_na_ordem_com_codigo_titulo_repositorios_risco_e_estado()
        {
            var issues = new[] { new IssueGitLabDto(37, "P1 — Painel de Fatias", "opened", ["em-execucao"]) };
            var specs = new Dictionary<string, string> { ["specs/feature/painel/01-painel-de-fatias.md"] = FixturesFatias.Ler("spec-p1-cabecalho.md") };
            var blocos = new Dictionary<long, string> { [37] = FixturesFatias.Ler("bloco-execucao-b2.md") };

            var fatias = MontadorDeFatias.Montar(Mapa, issues, SemMrs(), specs, blocos);

            fatias.Should().HaveCount(34);
            fatias[0].Codigo.Should().Be("B1");
            fatias[0].Estado.Estado.Should().Be(EstadoDaFatia.ProntaParaEntrega);
            var p1 = fatias.Single(f => f.Codigo == "P1");
            p1.Titulo.Should().StartWith("Painel de Fatias");
            p1.Repositorios.Should().Equal(RepositorioFatia.Api, RepositorioFatia.Frontend);
            p1.Risco.Should().Be("alto");
            p1.Estado.Estado.Should().Be(EstadoDaFatia.EmExecucao);
            p1.Estado.Progresso.Should().Be("2 de 3 tasks");
            p1.IssueMae.Should().Be(37);
            p1.SpecDisponivel.Should().BeTrue();
        }

        [Fact]
        public void Spec_ausente_ou_antiga_marca_formato_anterior_e_nao_derruba_a_lista()
        {
            var issues = new[] { new IssueGitLabDto(2, "B2 — Result pattern", "closed", []) };
            var specs = new Dictionary<string, string> { ["specs/feature/base/02-result-e-problem-details.md"] = FixturesFatias.Ler("spec-b2-formato-anterior.md") };

            var fatias = MontadorDeFatias.Montar(Mapa, issues, SemMrs(), specs, new Dictionary<long, string>());

            fatias.Should().HaveCount(34);
            var b2 = fatias.Single(f => f.Codigo == "B2");
            b2.Risco.Should().BeNull();
            b2.FormatoAnterior.Should().BeTrue();
            fatias.Single(f => f.Codigo == "B1").SpecDisponivel.Should().BeFalse();
        }

        [Fact]
        public void Mr_de_cada_repositorio_entra_na_derivacao()
        {
            var issues = new[] { new IssueGitLabDto(37, "P1 — Painel", "opened", ["em-execucao"]) };
            var mrs = new Dictionary<RepositorioFatia, IReadOnlyList<MergeRequestGitLabDto>>
            {
                [RepositorioFatia.Api] = [new MergeRequestGitLabDto(5, "P1 — Painel", "merged", "feature/p1-painel", null, null)],
                [RepositorioFatia.Frontend] = [new MergeRequestGitLabDto(6, "P1 — Painel", "opened", "feature/p1-painel", null, "success")],
            };

            var p1 = MontadorDeFatias.Montar(Mapa, issues, mrs, new Dictionary<string, string>(), new Dictionary<long, string>())
                .Single(f => f.Codigo == "P1");

            p1.Estado.Estado.Should().Be(EstadoDaFatia.EmRevisao);
            p1.Estado.Progresso.Should().Be("CI verde, aguardando você");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("lixo | sem | tabela")]
        public void Mapa_ilegivel_devolve_lista_vazia(string? mapa)
        {
            MontadorDeFatias.Montar(mapa, [], SemMrs(), new Dictionary<string, string>(), new Dictionary<long, string>())
                .Should().BeEmpty();
        }
    }
}
