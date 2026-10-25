using AwesomeAssertions;
using Esteira.Application.Sessao;

namespace Esteira.Application.Tests.Sessao
{
    // Critério 4 da B3: o retorno do login é só caminho relativo da mesma origem; o resto vira "/".
    public class ReturnUrlSeguroTests
    {
        [Theory]
        [InlineData("/entregas")]
        [InlineData("/entregas/7?aba=analises#topo")]
        [InlineData("/")]
        public void Caminho_relativo_da_mesma_origem_passa_como_veio(string valor)
            => ReturnUrlSeguro.Normalizar(valor).Should().Be(valor);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("//evil.example/x")]
        [InlineData("/\\evil.example")]
        [InlineData("https://evil.example/x")]
        [InlineData("http://localhost:4200/x")]
        [InlineData("javascript:alert(1)")]
        [InlineData("entregas")]
        [InlineData("/entregas\r\nSet-Cookie: x=1")]
        [InlineData("/\tevil.example")]
        public void Qualquer_outro_valor_vira_a_raiz(string? valor)
            => ReturnUrlSeguro.Normalizar(valor).Should().Be("/");
    }
}
