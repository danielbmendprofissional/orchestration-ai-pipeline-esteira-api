using Esteira.Domain.Common.Resultados;
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
            UrlValueObject.Criar(url).Value.Valor.Should().Be(esperado);
        }

        [Fact, DisplayName("Deve considerar maiúsculas no host e barra final como o mesmo valor")]
        public void Equals_HostEBarraFinal_DeveSerIgual()
        {
            UrlValueObject.Criar("HTTP://App.com/").Value.Should().Be(UrlValueObject.Criar("http://app.com").Value);
        }

        [Fact, DisplayName("Não deve ser igual quando o caminho for diferente")]
        public void Equals_CaminhosDiferentes_NaoDeveSerIgual()
        {
            UrlValueObject.Criar("https://app.com/a").Value.Should().NotBe(UrlValueObject.Criar("https://app.com/b").Value);
        }

        [Theory, DisplayName("Deve devolver falha url.invalida quando a URL for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("app.com")]
        [InlineData("ftp://app.com")]
        public void Criar_Invalida_DeveDevolverFalha(string url)
        {
            var resultado = UrlValueObject.Criar(url);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "url.invalida" && e.Type == TipoErro.Validation);
        }

        [Fact, DisplayName("Deve devolver falha url.longa-demais quando a URL ultrapassar 100 caracteres no padrão")]
        public void Criar_MaiorQue100_DeveDevolverFalha()
        {
            var resultado = UrlValueObject.Criar("https://app.com/" + new string('x', 100));

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "url.longa-demais" && e.Type == TipoErro.Validation);
        }

        [Fact, DisplayName("Deve aceitar URL de até 500 caracteres no DAST")]
        public void CriarDast_ate500_DeveCriar()
        {
            var url = "https://app.com/" + new string('x', 400);

            UrlValueObject.CriarDast(url).Value.Valor.Should().Be(url);
        }

        [Fact, DisplayName("Deve devolver falha url.longa-demais quando a URL ultrapassar 500 caracteres no DAST")]
        public void CriarDast_MaiorQue500_DeveDevolverFalha()
        {
            var resultado = UrlValueObject.CriarDast("https://app.com/" + new string('x', 500));

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("url.longa-demais");
        }
    }
}
