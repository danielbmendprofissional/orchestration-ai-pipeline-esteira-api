using AwesomeAssertions;
using Esteira.Application.Fatias;

namespace Esteira.Application.Tests.Fatias
{
    // P1.4, critérios 4 e 5: o mapa real do workspace é lido na ordem, com o que der, sem lançar.
    [Trait("Fatia", "P1")]
    public class LeitorDoMapaTests
    {
        private static readonly string Mapa = FixturesFatias.Ler("mapa-de-fatias.md");

        [Fact]
        public void Devolve_uma_linha_por_fatia_na_ordem_do_mapa()
        {
            var linhas = LeitorDoMapa.Ler(Mapa);

            linhas.Should().HaveCount(34);
            linhas.Select(l => l.Codigo).Take(4).Should().Equal("B1", "B2", "B3", "F0");
            linhas.Select(l => l.Codigo).Should().Contain(["F0.5", "E7b", "A1b", "P2b", "E3c"]);
        }

        [Fact]
        public void Le_titulo_spec_mae_status_e_repositorio_de_fatia_com_link()
        {
            var b1 = LeitorDoMapa.Ler(Mapa).Single(l => l.Codigo == "B1");

            b1.Titulo.Should().Be("Paginação `X-*` (ADR-0002)");
            b1.CaminhoSpec.Should().Be("specs/feature/base/03-paginacao.md");
            b1.IssueMae.Should().Be(3);
            b1.Feito.Should().BeTrue();
            b1.StatusReconhecido.Should().BeTrue();
            b1.Repositorios.Should().Equal(RepositorioFatia.Api);
            b1.FormatoAnterior.Should().BeFalse();
        }

        [Fact]
        public void Fatia_api_mais_frontend_traz_os_dois_repositorios_e_ignora_infra_e_template()
        {
            var linhas = LeitorDoMapa.Ler(Mapa);

            linhas.Single(l => l.Codigo == "P1").Repositorios.Should().Equal(RepositorioFatia.Api, RepositorioFatia.Frontend);
            linhas.Single(l => l.Codigo == "E4").Repositorios.Should().Equal(RepositorioFatia.Api);
            linhas.Single(l => l.Codigo == "E1a").Repositorios.Should().Equal(RepositorioFatia.Frontend, RepositorioFatia.Api);
            linhas.Single(l => l.Codigo == "P1").Status.Should().Be("—");
            linhas.Single(l => l.Codigo == "P1").Feito.Should().BeFalse();
            linhas.Single(l => l.Codigo == "P1").StatusReconhecido.Should().BeTrue();
        }

        [Fact]
        public void Fatia_sem_link_de_spec_e_sem_mae_vem_sem_caminho_nem_issue()
        {
            var f0 = LeitorDoMapa.Ler(Mapa).Single(l => l.Codigo == "F0");

            f0.Titulo.Should().Be("Bootstrap do frontend (ADR-0005/0006)");
            f0.CaminhoSpec.Should().BeNull();
            f0.IssueMae.Should().BeNull();
        }

        [Fact]
        public void Codigo_com_ponto_e_lido_inteiro()
        {
            LeitorDoMapa.Ler(Mapa).Single(l => l.Codigo == "F0.5").IssueMae.Should().Be(19);
        }

        [Fact]
        public void Status_desconhecido_vira_texto_e_linha_desconhecida_e_ignorada()
        {
            var texto = "## Mapa de fatias\n\n| Status | # | Fatia | Repositório |\n|---|---|---|---|\n"
                + "| em pausa | Z9 | Algo — [`x.md`](feature/x.md) · #70 | api |\n"
                + "isto nao e uma linha de tabela\n"
                + "| so | duas |\n"
                + "| — | Z10 | Outra | frontend |\n";

            var linhas = LeitorDoMapa.Ler(texto);

            linhas.Select(l => l.Codigo).Should().Equal("Z9", "Z10");
            linhas[0].Status.Should().Be("em pausa");
            linhas[0].StatusReconhecido.Should().BeFalse();
            linhas[0].Feito.Should().BeFalse();
        }

        [Fact]
        public void Linha_com_menos_colunas_que_o_padrao_mostra_o_que_da_com_a_marca_de_formato_anterior()
        {
            var linhas = LeitorDoMapa.Ler("## Mapa de fatias\n\n| feito | Z1 | Antiga |\n");

            linhas.Should().ContainSingle();
            linhas[0].Titulo.Should().Be("Antiga");
            linhas[0].FormatoAnterior.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("| | | |\n|||\n| a | b")]
        [InlineData("## Mapa de fatias\n| ||||||||||| \\|\\| `")]
        public void Entrada_quebrada_nunca_lanca(string? texto)
        {
            var agir = () => LeitorDoMapa.Ler(texto);

            agir.Should().NotThrow();
        }
    }
}
