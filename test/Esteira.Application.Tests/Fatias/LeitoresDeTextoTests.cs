using AwesomeAssertions;
using Esteira.Application.Fatias;

namespace Esteira.Application.Tests.Fatias
{
    // P1.4, critérios 4, 5 e 7: spec, títulos, bloco de execução, branch e commit contra as fixtures reais.
    [Trait("Fatia", "P1")]
    public class LeitoresDeTextoTests
    {
        [Fact]
        public void Spec_no_padrao_traz_risco_flag_e_base_sem_o_comentario()
        {
            var c = LeitorDeSpec.Ler(FixturesFatias.Ler("spec-p1-cabecalho.md"));

            c.Titulo.Should().Be("Painel de Fatias");
            c.Risco.Should().Be("alto");
            c.Flag.Should().BeTrue();
            c.Base.Should().StartWith("workspace@7f67008");
            c.FormatoAnterior.Should().BeFalse();
        }

        [Fact]
        public void Spec_antiga_sem_cabecalho_do_padrao_vem_marcada_e_sem_lancar()
        {
            var c = LeitorDeSpec.Ler(FixturesFatias.Ler("spec-b2-formato-anterior.md"));

            c.Titulo.Should().Be("Result pattern + Problem Details");
            c.Risco.Should().BeNull();
            c.Base.Should().BeNull();
            c.Flag.Should().BeTrue();
            c.FormatoAnterior.Should().BeTrue();
        }

        [Fact]
        public void Spec_com_so_o_risco_e_parcial()
        {
            var c = LeitorDeSpec.Ler(FixturesFatias.Ler("spec-b3-parcial.md"));

            c.Risco.Should().Be("alto");
            c.Base.Should().BeNull();
            c.FormatoAnterior.Should().BeTrue();
        }

        [Theory]
        [InlineData("# 01 — X\n\nrisco: baixo\nbase: a\nflag: não\n", false)]
        [InlineData("# 01 — X\n\nrisco: baixo\nbase: a\nflag: nao\n", false)]
        [InlineData("# 01 — X\n\nrisco: baixo\nbase: a\nflag: sim\n", true)]
        public void Flag_nao_desliga_e_sim_liga(string texto, bool esperado)
        {
            LeitorDeSpec.Ler(texto).Flag.Should().Be(esperado);
        }

        [Fact]
        public void Risco_so_vale_no_cabecalho_nao_no_corpo()
        {
            var c = LeitorDeSpec.Ler("# 01 — X\n\n## Contexto\nrisco: alto\n");

            c.Risco.Should().BeNull();
        }

        [Fact]
        public void Spec_nula_nao_lanca()
        {
            LeitorDeSpec.Ler(null).FormatoAnterior.Should().BeTrue();
        }

        [Fact]
        public void Titulo_da_mae_da_fixture()
        {
            var t = LeitorDeTitulo.Ler(FixturesFatias.Ler("titulo-mae-b2.txt"));

            t.Should().NotBeNull();
            t!.Codigo.Should().Be("B2");
            t.NumeroTask.Should().BeNull();
            t.Titulo.Should().Be("Result pattern + Problem Details (ADR-0003)");
        }

        [Fact]
        public void Titulo_da_filha_da_fixture()
        {
            var t = LeitorDeTitulo.Ler(FixturesFatias.Ler("titulo-filha-b2-5.txt"));

            t!.Codigo.Should().Be("B2");
            t.NumeroTask.Should().Be(5);
        }

        [Theory]
        [InlineData("E7b — Aguardando liberação", "E7b", null)]
        [InlineData("B2-L — Limpeza", "B2-L", null)]
        [InlineData("B2-L.1 — Limpeza", "B2-L", 1)]
        public void Titulo_de_outras_formas_de_codigo(string titulo, string codigo, int? task)
        {
            var t = LeitorDeTitulo.Ler(titulo);

            t!.Codigo.Should().Be(codigo);
            t.NumeroTask.Should().Be(task);
        }

        [Fact]
        public void Codigo_conhecido_com_ponto_nao_e_confundido_com_task()
        {
            var t = LeitorDeTitulo.Ler("F0.5 — Shell", ["F0", "F0.5"]);

            t!.Codigo.Should().Be("F0.5");
            t.NumeroTask.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Hotfix: corrige o login")]
        [InlineData("sem codigo nenhum")]
        public void Titulo_fora_do_padrao_devolve_null(string? titulo)
        {
            LeitorDeTitulo.Ler(titulo).Should().BeNull();
        }

        [Fact]
        public void Bloco_da_fixture_traz_cabecalho_tasks_e_linhas_finais()
        {
            var b = LeitorDoBlocoDeExecucao.Ler(FixturesFatias.Ler("bloco-execucao-b2.md"));

            b.Should().NotBeNull();
            b!.Modo.Should().Be("Claude");
            b.Inicio.Should().Be("2026-10-25");
            b.Area.Should().Be(@"C:\ws\esteira");
            b.Tasks.Should().HaveCount(3);
            b.Tasks[0].Codigo.Should().Be("B2");
            b.Tasks[0].NumeroTask.Should().Be(1);
            b.Tasks[0].IssueFilha.Should().Be(4);
            b.Tasks[0].Feita.Should().BeTrue();
            b.Tasks[0].Sha.Should().Be("0b4bd79");
            b.Tasks[0].Executor.Should().Be("pai (Claude)");
            b.Tasks[1].Sha.Should().Be("3fa91c2");
            b.Tasks[1].Executor.Should().Be("backend (omni-pago)");
            b.Tasks[1].ReviewPass.Should().BeTrue();
            b.Tasks[2].Feita.Should().BeFalse();
            b.Tasks[2].EmAndamento.Should().BeTrue();
            b.Tasks[2].Sha.Should().BeNull();
            b.SuiteCompleta.Should().BeFalse();
            b.Review.Should().BeFalse();
            b.Mr.Should().BeFalse();
            b.TasksFeitas.Should().Be(2);
            b.Progresso.Should().Be("2 de 3 tasks");
        }

        [Fact]
        public void Bloco_ausente_ou_vazio()
        {
            LeitorDoBlocoDeExecucao.Ler("so texto da mae").Should().BeNull();
            LeitorDoBlocoDeExecucao.Ler(null).Should().BeNull();
            LeitorDoBlocoDeExecucao.Ler("<!-- execucao -->\n<!-- /execucao -->")!.Progresso.Should().BeNull();
        }

        [Fact]
        public void Linha_desconhecida_no_bloco_e_ignorada_e_marcador_sem_fechamento_nao_lanca()
        {
            var b = LeitorDoBlocoDeExecucao.Ler("<!-- execucao -->\n- [x] B2.1 #4 — `abc1234` · pai\nlinha estranha\n- [ ] ??? \n- [x] Suite completa\n");

            b!.Tasks.Should().ContainSingle();
            b.SuiteCompleta.Should().BeTrue();
        }

        [Fact]
        public void Branch_de_fatia_traz_codigo_minusculo_e_slug()
        {
            var b = LeitorDeBranch.Ler("feature/b2-result-e-problem-details");

            b.Should().NotBeNull();
            b!.Codigo.Should().Be("b2");
            b.Slug.Should().Be("result-e-problem-details");
            LeitorDeBranch.Ler("main").Should().BeNull();
            LeitorDeBranch.Ler(null).Should().BeNull();
        }

        [Fact]
        public void Commit_da_fixture_traz_tipo_codigo_task_e_filha_e_executor()
        {
            var c = LeitorDeCommit.Ler(FixturesFatias.Ler("commit-task-b2-5.txt"));

            c.Should().NotBeNull();
            c!.Tipo.Should().Be("refactor");
            c.Codigo.Should().Be("B2");
            c.NumeroTask.Should().Be(5);
            c.IssueFilha.Should().Be(8);
            c.Executor.Should().Be("backend (sonnet)");
            LeitorDeCommit.Ler("mensagem solta").Should().BeNull();
        }
    }
}
