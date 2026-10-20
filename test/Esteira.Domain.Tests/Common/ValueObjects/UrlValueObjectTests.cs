using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Common.ValueObjects
{
    public class UrlValueObjectTests
    {
        [Theory, DisplayName("Deve normalizar removendo barra final, query e espaços")]
        [InlineData("https://app.com/", "https://app.com")]
        [InlineData(" https://app.com/x/ ", "https://app.com/x")]
        [InlineData("https://app.com/x?a=1", "https://app.com/x")]
        public void Criar_DeveNormalizar(string url, string esperado)
        {
            UrlValueObject.Criar(url).Valor.Should().Be(esperado);
        }

        [Fact, DisplayName("Deve considerar maiúsculas no host e barra final como o mesmo valor")]
        public void Equals_HostEBarraFinal_DeveSerIgual()
        {
            UrlValueObject.Criar("HTTP://App.com/").Should().Be(UrlValueObject.Criar("http://app.com"));
        }

        [Fact, DisplayName("Não deve ser igual quando o caminho for diferente")]
        public void Equals_CaminhosDiferentes_NaoDeveSerIgual()
        {
            UrlValueObject.Criar("https://app.com/a").Should().NotBe(UrlValueObject.Criar("https://app.com/b"));
        }

        [Fact, DisplayName("Deve manter o comportamento do Normalize estático")]
        public void Normalize_DeveRetornarValor()
        {
            UrlValueObject.Normalize("https://app.com/x/").Should().Be("https://app.com/x");
        }

        [Theory, DisplayName("Deve lançar exceção quando a URL for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("app.com")]
        [InlineData("ftp://app.com")]
        public void Criar_Invalida_DeveLancarDomainException(string url)
        {
            var act = () => UrlValueObject.Criar(url);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a URL ultrapassar 100 caracteres no padrão")]
        public void Criar_MaiorQue100_DeveLancarDomainException()
        {
            var act = () => UrlValueObject.Criar("https://app.com/" + new string('x', 100));

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve aceitar URL de até 500 caracteres no DAST")]
        public void CriarDast_ate500_DeveCriar()
        {
            var url = "https://app.com/" + new string('x', 400);

            UrlValueObject.CriarDast(url).Valor.Should().Be(url);
        }

        [Fact, DisplayName("Deve lançar exceção quando a URL ultrapassar 500 caracteres no DAST")]
        public void CriarDast_MaiorQue500_DeveLancarDomainException()
        {
            var act = () => UrlValueObject.CriarDast("https://app.com/" + new string('x', 500));

            act.Should().Throw<DomainException>();
        }
    }
}
