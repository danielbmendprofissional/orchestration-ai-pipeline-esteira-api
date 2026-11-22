using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // P1.4, critérios 6 a 9: estado derivado em tabela. MRs no formato "repo:estado[:pipeline]" separadas por vírgula.
    [Trait("Fatia", "P1")]
    public class DerivadorDeEstadoTests
    {
        private const string Codigo = "X1";

        private static ResultadoDeEstado Derivar(string status, bool temSpec, string mae, string? bloco, string repos, string mrs)
        {
            var repositorios = repos.Split('+').Select(r => r == "api" ? RepositorioFatia.Api : RepositorioFatia.Frontend).ToList();
            var linha = new LinhaDoMapa(Codigo, "Titulo", status, status is "feito" or "—", status == "feito",
                temSpec ? "specs/feature/x/01-x.md" : null, mae == "nenhuma" ? null : 10, repositorios, repos, false);
            var issue = mae switch
            {
                "nenhuma" => null,
                "em-execucao" => new IssueGitLabDto(10, "X1 — Titulo", "opened", ["em-execucao"]),
                _ => new IssueGitLabDto(10, "X1 — Titulo", "opened", ["backend"]),
            };
            var bloco2 = bloco is null ? null : LeitorDoBlocoDeExecucao.Ler(Bloco(bloco));
            var lista = string.IsNullOrEmpty(mrs) ? [] : mrs.Split(',').Select((s, i) => Mr(s, i)).ToList();

            return DerivadorDeEstado.Derivar(linha, issue, bloco2, lista);
        }

        private static string Bloco(string feitasDeTotal)
        {
            var p = feitasDeTotal.Split('/');
            var feitas = int.Parse(p[0]);
            var total = int.Parse(p[1]);
            var linhas = Enumerable.Range(1, total).Select(i => $"- [{(i <= feitas ? "x" : " ")}] X1.{i} #{20 + i} — `abc123{i}` · pai");
            return "<!-- execucao -->\n" + string.Join("\n", linhas) + "\n<!-- /execucao -->";
        }

        private static MrDaFatia Mr(string spec, int i)
        {
            var p = spec.Split(':');
            var repo = p[0] == "api" ? RepositorioFatia.Api : RepositorioFatia.Frontend;
            var pipeline = p.Length > 2 ? p[2] : null;
            return new MrDaFatia(repo, new MergeRequestGitLabDto(100 + i, "X1 — Titulo", p[1], "feature/x1-titulo", null, pipeline));
        }

        private static MrDaFatia Mr(string spec) => Mr(spec, 0);

        [Theory]
        // critério 6
        [InlineData("—", false, "nenhuma", null, "api", "", EstadoDaFatia.Planejada, null, false)]
        [InlineData("—", true, "sem-label", null, "api", "", EstadoDaFatia.ComSpec, null, false)]
        // critério 7
        [InlineData("—", true, "em-execucao", "3/7", "api", "", EstadoDaFatia.EmExecucao, "3 de 7 tasks", false)]
        [InlineData("—", true, "em-execucao", "0/4", "api+frontend", "", EstadoDaFatia.EmExecucao, "0 de 4 tasks", false)]
        [InlineData("—", true, "em-execucao", null, "api", "", EstadoDaFatia.EmExecucao, null, true)]
        // critério 8
        [InlineData("—", true, "em-execucao", "7/7", "api", "api:opened:success", EstadoDaFatia.EmRevisao, "CI verde, aguardando você", false)]
        [InlineData("—", true, "em-execucao", "7/7", "api", "api:opened:failed", EstadoDaFatia.EmRevisao, "CI vermelho", false)]
        [InlineData("—", true, "em-execucao", "7/7", "api", "api:opened:running", EstadoDaFatia.EmRevisao, "CI rodando", false)]
        [InlineData("—", true, "em-execucao", "7/7", "api", "api:opened:pending", EstadoDaFatia.EmRevisao, "CI rodando", false)]
        [InlineData("—", true, "sem-label", null, "api", "api:opened", EstadoDaFatia.EmRevisao, null, false)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:merged,frontend:opened:running", EstadoDaFatia.EmRevisao, "CI rodando", false)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:opened:success,frontend:opened:failed", EstadoDaFatia.EmRevisao, "CI vermelho", false)]
        [InlineData("—", true, "sem-label", null, "api", "api:closed", EstadoDaFatia.EmRevisao, "em correção", true)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:merged,frontend:closed", EstadoDaFatia.EmRevisao, "em correção", true)]
        [InlineData("—", true, "sem-label", null, "api", "api:closed,api:opened:running", EstadoDaFatia.EmRevisao, "CI rodando", false)]
        // critério 9
        [InlineData("—", true, "sem-label", null, "api", "api:merged", EstadoDaFatia.ProntaParaEntrega, null, false)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:merged,frontend:merged", EstadoDaFatia.ProntaParaEntrega, null, false)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:closed,api:merged,frontend:merged", EstadoDaFatia.ProntaParaEntrega, null, false)]
        [InlineData("—", true, "sem-label", null, "api+frontend", "api:merged", EstadoDaFatia.ComSpec, null, false)]
        [InlineData("—", true, "em-execucao", "2/5", "api+frontend", "api:merged", EstadoDaFatia.EmExecucao, "2 de 5 tasks", false)]
        [InlineData("feito", true, "sem-label", null, "api", "", EstadoDaFatia.ProntaParaEntrega, null, false)]
        [InlineData("feito", false, "nenhuma", null, "api", "", EstadoDaFatia.ProntaParaEntrega, null, false)]
        // status desconhecido não muda a derivação
        [InlineData("em pausa", false, "nenhuma", null, "api", "", EstadoDaFatia.Planejada, null, false)]
        public void Deriva_o_estado_e_o_progresso(
            string status, bool temSpec, string mae, string? bloco, string repos, string mrs,
            EstadoDaFatia estado, string? progresso, bool formatoAnteriorOuCorrecao)
        {
            var r = Derivar(status, temSpec, mae, bloco, repos, mrs);

            r.Estado.Should().Be(estado);
            r.Progresso.Should().Be(progresso);
            (r.EmCorrecao || r.FormatoAnterior).Should().Be(formatoAnteriorOuCorrecao);
        }

        [Fact]
        public void Em_correcao_e_marcado_so_quando_ha_mr_fechada_sem_merge()
        {
            var r = Derivar("—", true, "sem-label", null, "api", "api:closed");

            r.EmCorrecao.Should().BeTrue();
            r.FormatoAnterior.Should().BeFalse();
        }

        [Fact]
        public void Mr_de_outra_fatia_e_ignorada()
        {
            var linha = new LinhaDoMapa("X1", "T", "—", true, false, "specs/x.md", 10, [RepositorioFatia.Api], "api", false);
            var mae = new IssueGitLabDto(10, "X1 — T", "opened", ["em-execucao"]);
            var outra = new MrDaFatia(RepositorioFatia.Api, new MergeRequestGitLabDto(1, "X2 — Outra", "opened", "feature/x2-o", null, "success"));
            var minha = new MrDaFatia(RepositorioFatia.Api, new MergeRequestGitLabDto(2, "X1 — T", "merged", "feature/x1-t", null, null));

            DerivadorDeEstado.Derivar(linha, mae, null, [outra]).Estado.Should().Be(EstadoDaFatia.EmExecucao);
            DerivadorDeEstado.Derivar(linha, mae, null, [outra, minha]).Estado.Should().Be(EstadoDaFatia.ProntaParaEntrega);
        }

        [Fact]
        public void Estado_nunca_lanca_com_entradas_nulas_ou_estados_desconhecidos()
        {
            var linha = new LinhaDoMapa("X1", "T", "???", false, false, null, null, [], "", true);
            var mr = new MrDaFatia(RepositorioFatia.Api, new MergeRequestGitLabDto(1, null!, "locked", null!, null, "xyz"));

            var agir = () => DerivadorDeEstado.Derivar(linha, null, null, [mr]);

            agir.Should().NotThrow();
        }

        [Fact]
        public void Resolve_a_mae_pelo_numero_do_mapa_ou_pelo_titulo()
        {
            var issues = new[]
            {
                new IssueGitLabDto(10, "X1 — Titulo", "opened", []),
                new IssueGitLabDto(11, "X1.1 — Filha", "opened", []),
                new IssueGitLabDto(12, "X2 — Outra", "opened", []),
            };
            var comNumero = new LinhaDoMapa("X2", "T", "—", true, false, null, 12, [], "", false);
            var semNumero = new LinhaDoMapa("X1", "T", "—", true, false, null, null, [], "", false);
            var semNada = new LinhaDoMapa("X9", "T", "—", true, false, null, null, [], "", false);

            DerivadorDeEstado.ResolverMae(comNumero, issues)!.Iid.Should().Be(12);
            DerivadorDeEstado.ResolverMae(semNumero, issues)!.Iid.Should().Be(10);
            DerivadorDeEstado.ResolverMae(semNada, issues).Should().BeNull();
        }
    }
}
