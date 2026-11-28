using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // X3 (spec 02-teto-de-custo-dos-agentes.md, "Dados"): teto de custo por execução de agente, nulo = padrão da Esteira.
    [Trait("Fatia", "X3")]
    public class TetoCustoDaAplicacaoTests
    {
        private static AplicacaoEntity Nova() => AplicacaoEntity.Criar("Valuto", "VLT", 10, "valuto").Value;

        [Fact, DisplayName("Deve nascer sem teto (usa o padrão da Esteira)")]
        public void Aplicacao_Criar_SemTeto()
        {
            Nova().TetoCustoAgentesUsd.Should().BeNull();
        }

        [Theory, DisplayName("Deve aceitar teto dentro da faixa, inclusive nos limites")]
        [InlineData("0.10")]
        [InlineData("2.50")]
        [InlineData("50.00")]
        [InlineData("5")]
        public void Aplicacao_DefinirTetoCusto_Valido(string texto)
        {
            var valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
            var a = Nova();

            var r = a.DefinirTetoCusto(valor);

            r.IsSuccess.Should().BeTrue();
            a.TetoCustoAgentesUsd.Should().Be(valor);
        }

        [Theory, DisplayName("Deve recusar teto fora da faixa ou com mais de 2 casas e manter o anterior")]
        [InlineData("0.09")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("50.01")]
        [InlineData("2.505")]
        [InlineData("0.001")]
        public void Aplicacao_DefinirTetoCusto_Invalido(string texto)
        {
            var valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
            var a = Nova();
            a.DefinirTetoCusto(3.00m);

            var r = a.DefinirTetoCusto(valor);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.teto-invalido");
            a.TetoCustoAgentesUsd.Should().Be(3.00m);
        }

        [Fact, DisplayName("Deve voltar ao padrão (nulo) quando o teto é null")]
        public void Aplicacao_DefinirTetoCusto_NullRestauraPadrao()
        {
            var a = Nova();
            a.DefinirTetoCusto(4.00m);

            var r = a.DefinirTetoCusto(null);

            r.IsSuccess.Should().BeTrue();
            a.TetoCustoAgentesUsd.Should().BeNull();
        }
    }
}
