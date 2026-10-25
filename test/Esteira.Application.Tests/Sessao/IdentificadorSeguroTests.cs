using AwesomeAssertions;
using Esteira.Application.Sessao;

namespace Esteira.Application.Tests.Sessao
{
    // O id da sessão e o state do login vêm do cliente (cookie, query) e viram parte de chave do Redis:
    // só a forma que a API gera (base64url, 16 a 64 caracteres) passa.
    public class IdentificadorSeguroTests
    {
        [Theory]
        [InlineData("abcdefghijklmnop")]
        [InlineData("Zm9vYmFyYmF6cXV4MTIzNDU2Nzg5MC1fYWJj")]
        public void Base64url_de_tamanho_razoavel_passa(string valor)
            => IdentificadorSeguro.Valido(valor).Should().BeTrue();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("curto")]
        [InlineData("a:b:c:dddddddddddddddd")]
        [InlineData("tem espaco nesse valor aqui")]
        [InlineData("ok-ok-ok-ok-ok-ok-\n")]
        public void Vazio_curto_ou_com_caractere_fora_do_alfabeto_nao_passa(string valor)
            => IdentificadorSeguro.Valido(valor).Should().BeFalse();

        [Fact]
        public void Maior_que_64_caracteres_nao_passa()
            => IdentificadorSeguro.Valido(new string('a', 65)).Should().BeFalse();
    }
}
