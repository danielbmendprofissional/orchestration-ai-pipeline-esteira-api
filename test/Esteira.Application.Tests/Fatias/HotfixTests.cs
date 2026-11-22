using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // P1.7, critério 16: pedido de hotfix (ADR-0027) lido do GitLab; parser e derivação puros, com fixtures reais.
    [Trait("Fatia", "P1")]
    public class HotfixTests
    {
        private static readonly string Titulo = FixturesFatias.Ler("titulo-issue-hotfix.txt");
        private static readonly string Branch = FixturesFatias.Ler("branch-hotfix.txt");
        private static readonly string Descricao = FixturesFatias.Ler("descricao-mr-hotfix.txt");

        private static IssueGitLabDto Pedido(long iid = 51, string? titulo = null, params string[] labels)
            => new(iid, titulo ?? Titulo, "opened", labels.Length == 0 ? ["ready-for-human"] : labels);

        private static MergeRequestGitLabDto Mr(string estado, long iid = 9, string? branch = null, string? descricao = null, string titulo = "Corrige juros")
            => new(iid, titulo, estado, branch ?? Branch, descricao ?? Descricao, null);

        private static IReadOnlyList<HotfixLido> Derivar(IssueGitLabDto[] issues, params (RepositorioFatia, MergeRequestGitLabDto)[] mrs)
            => DerivadorDeHotfix.Derivar(issues, mrs);

        [Fact]
        public void Titulo_real_vira_o_motivo()
            => LeitorDeHotfix.Motivo(Titulo).Should().Be("Arredondamento de juros errado no boleto");

        [Theory]
        [InlineData("Hotfix:")]
        [InlineData("Hotfix:   ")]
        [InlineData("B2 — Hotfix: não é pedido")]
        [InlineData("Correção urgente")]
        [InlineData(null)]
        public void Titulo_fora_do_padrao_nao_e_pedido(string? titulo)
            => LeitorDeHotfix.Motivo(titulo).Should().BeNull();

        [Fact]
        public void Issue_sem_label_ready_for_human_nao_e_pedido()
            => Derivar([Pedido(labels: "backend")]).Should().BeEmpty();

        [Fact]
        public void Sem_mr_o_pedido_aguarda_a_correcao()
        {
            var h = Derivar([Pedido()]).Single();
            h.Estado.Should().Be(EstadoDoHotfix.AguardandoCorrecao);
            h.Mr.Should().BeNull();
            h.Iid.Should().Be(51);
            h.Motivo.Should().Be("Arredondamento de juros errado no boleto");
        }

        [Fact]
        public void Mr_hotfix_aberta_que_cita_a_issue_mostra_mr_aberta()
        {
            var h = Derivar([Pedido()], (RepositorioFatia.Api, Mr("opened"))).Single();
            h.Estado.Should().Be(EstadoDoHotfix.MrAberta);
            h.Mr.Should().BeEquivalentTo(new MrDoHotfix(RepositorioFatia.Api, 9, "Corrige juros", "opened", Branch));
        }

        [Fact]
        public void Mr_hotfix_mergeada_deixa_pronto_para_entrega()
            => Derivar([Pedido()], (RepositorioFatia.Api, Mr("merged"))).Single().Estado.Should().Be(EstadoDoHotfix.ProntoParaEntrega);

        [Fact]
        public void Mr_fechada_sem_merge_volta_a_aguardar_a_correcao()
        {
            var h = Derivar([Pedido()], (RepositorioFatia.Api, Mr("closed"))).Single();
            h.Estado.Should().Be(EstadoDoHotfix.AguardandoCorrecao);
            h.Mr.Should().BeNull();
        }

        [Fact]
        public void Mr_fechada_e_depois_nova_aberta_mostra_a_aberta()
            => Derivar([Pedido()], (RepositorioFatia.Api, Mr("closed", 8)), (RepositorioFatia.Api, Mr("opened", 10)))
                .Single().Mr!.Iid.Should().Be(10);

        [Fact]
        public void Mergeada_vale_mais_que_aberta()
            => Derivar([Pedido()], (RepositorioFatia.Api, Mr("opened", 10)), (RepositorioFatia.Api, Mr("merged", 9)))
                .Single().Estado.Should().Be(EstadoDoHotfix.ProntoParaEntrega);

        [Fact]
        public void Mr_de_feature_que_cita_a_issue_nao_conta()
            => Derivar([Pedido()], (RepositorioFatia.Api, Mr("opened", branch: "feature/b2-algo")))
                .Single().Estado.Should().Be(EstadoDoHotfix.AguardandoCorrecao);

        [Fact]
        public void Mr_hotfix_que_nao_cita_a_issue_nao_conta()
            => Derivar([Pedido()], (RepositorioFatia.Api, Mr("opened", descricao: "Closes demo/demo-workspace#5")))
                .Single().Estado.Should().Be(EstadoDoHotfix.AguardandoCorrecao);

        [Theory]
        [InlineData("Closes #51")]
        [InlineData("Resolve demo/demo-workspace#51.")]
        [InlineData("ver https://gitlab.exemplo.com/demo/demo-workspace/-/issues/51")]
        public void Citacoes_aceitas(string descricao)
            => Derivar([Pedido()], (RepositorioFatia.Frontend, Mr("opened", descricao: descricao)))
                .Single().Mr!.Repositorio.Should().Be(RepositorioFatia.Frontend);

        [Fact]
        public void Citacao_no_titulo_da_mr_tambem_vale()
            => Derivar([Pedido()], (RepositorioFatia.Api, new MergeRequestGitLabDto(9, "Hotfix #51 juros", "opened", Branch, null, null)))
                .Single().Estado.Should().Be(EstadoDoHotfix.MrAberta);

        [Fact]
        public void Numero_que_so_comeca_igual_nao_e_citacao()
            => Derivar([Pedido(5)], (RepositorioFatia.Api, Mr("opened", descricao: "Closes #51")))
                .Single().Estado.Should().Be(EstadoDoHotfix.AguardandoCorrecao);

        [Fact]
        public void Varios_pedidos_vem_do_mais_novo_ao_mais_antigo_e_texto_e_cortado()
        {
            var longo = "Hotfix: " + new string('x', 500);
            var r = Derivar([Pedido(3), Pedido(7, longo)]);
            r.Select(x => x.Iid).Should().Equal(7, 3);
            r[0].Motivo.Length.Should().Be(200);
        }

        [Fact]
        public void Texto_com_marcacao_fica_como_texto()
            => Derivar([Pedido(titulo: "Hotfix: <script>alert(1)</script>")]).Single().Motivo.Should().Be("<script>alert(1)</script>");
    }
}
