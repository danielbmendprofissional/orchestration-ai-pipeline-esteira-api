using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Fatias.Decisoes;

namespace Esteira.Application.Tests.Fatias
{
    // P2b.1, critério 4: o texto que o humano escreveu no Reprovar vai para a MR em citação, cada linha com "> ",
    // para que uma linha como `/merge` ou `/close` não vire ação rápida do GitLab executada como o bot.
    [Trait("Fatia", "P2b")]
    public class CitacaoDoHumanoTests
    {
        [Fact, DisplayName("Toda linha do corpo citado começa com '>', inclusive as do humano e as em branco")]
        public void TodasAsLinhasCitadas()
        {
            var corpo = CitacaoDoHumano.Citar("Fulano de Tal", "primeira\n\nterceira");

            corpo.Split('\n').Should().OnlyContain(linha => linha.StartsWith('>'));
        }

        [Fact, DisplayName("Uma linha '/merge' vira '> /merge' e não fica no começo de linha (ação rápida neutralizada)")]
        public void QuickActionNeutralizada()
        {
            var corpo = CitacaoDoHumano.Citar("Fulano", "segue o ajuste\n/merge");

            corpo.Should().Contain("> /merge");
            corpo.Split('\n').Should().NotContain(linha => linha.StartsWith('/'));
        }

        [Fact, DisplayName("O nome do humano aparece no corpo e também é neutralizado (não vira ação rápida nem quebra a citação)")]
        public void NomeNeutralizado()
        {
            var corpo = CitacaoDoHumano.Citar("/close", "ok");

            corpo.Should().Contain("/close");
            corpo.Split('\n').Should().OnlyContain(linha => linha.StartsWith('>'));
            corpo.Split('\n').Should().NotContain(linha => linha.StartsWith('/'));
        }

        [Fact, DisplayName("Nome com quebra de linha não abre uma linha solta (fora da citação)")]
        public void NomeComQuebraDeLinha()
        {
            var corpo = CitacaoDoHumano.Citar("Fulano\n/close", "ok");

            corpo.Split('\n').Should().OnlyContain(linha => linha.StartsWith('>'));
        }
    }
}
