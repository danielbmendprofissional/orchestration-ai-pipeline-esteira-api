using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Tests.Aplicacoes
{
    // X3.2: EditarTetoCusto é só MOR; o padrão da Esteira sai de Pipeline:TetoCustoPadraoUsd (ausente/inválido/fora da faixa -> 2.00).
    [Trait("Fatia", "X3")]
    public class TetoCustoPermissaoEPadraoTests
    {
        [Theory, DisplayName("Critério 4: nenhum papel da Aplicação pode EditarTetoCusto")]
        [InlineData(PapelAplicacao.Membro)]
        [InlineData(PapelAplicacao.Analista)]
        [InlineData(PapelAplicacao.AprovadorTecnico)]
        public void Papel_NaoPode(PapelAplicacao papel)
            => new AcessoAplicacao(false, [papel]).Pode(OperacaoAplicacao.EditarTetoCusto).Should().BeFalse();

        [Fact, DisplayName("Critério 4: MOR pode EditarTetoCusto")]
        public void Mor_Pode()
            => new AcessoAplicacao(true, []).Pode(OperacaoAplicacao.EditarTetoCusto).Should().BeTrue();

        [Theory, DisplayName("Padrão: valor válido na faixa é usado; ausente, inválido ou fora da faixa vira 2.00")]
        [InlineData("3.50", "3.50")]
        [InlineData("0.10", "0.10")]
        [InlineData("50", "50")]
        [InlineData("50.00", "50.00")]
        [InlineData(null, "2.00")]
        [InlineData("", "2.00")]
        [InlineData("abc", "2.00")]
        [InlineData("0", "2.00")]
        [InlineData("0.09", "2.00")]
        [InlineData("50.01", "2.00")]
        [InlineData("-1", "2.00")]
        [InlineData("1,5", "2.00")]
        public void Padrao_Resolve(string? configurado, string esperado)
            => TetoCustoPadrao.Resolver(configurado).Should().Be(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
    }
}
