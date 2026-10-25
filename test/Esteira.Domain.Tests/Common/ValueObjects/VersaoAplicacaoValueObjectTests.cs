using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Common.ValueObjects
{
    public class VersaoAplicacaoValueObjectTests
    {
        [Theory, DisplayName("Deve extrair a versão major da tag")]
        [InlineData("v2.3.1", 2)]
        [InlineData("V10.0", 10)]
        [InlineData("3", 3)]
        public void DaTag_DeveExtrairMajor(string tag, int esperado)
        {
            VersaoAplicacaoValueObject.DaTag(tag).Value.Major.Should().Be(esperado);
        }

        [Fact, DisplayName("Deve aceitar somente tags da mesma versão major")]
        public void AceitaTag_DeveCompararMajor()
        {
            var versao = VersaoAplicacaoValueObject.Criar("v2").Value;

            versao.AceitaTag("v2.9.9").Should().BeTrue();
            versao.AceitaTag("v3.0.0").Should().BeFalse();
        }

        [Theory, DisplayName("Deve devolver false quando a tag for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void AceitaTag_TagInvalida_DeveDevolverFalse(string tag)
        {
            VersaoAplicacaoValueObject.Criar(2).Value.AceitaTag(tag).Should().BeFalse();
        }

        [Fact, DisplayName("Deve comparar as versões major")]
        public void EhMenorQue_DeveCompararMajor()
        {
            VersaoAplicacaoValueObject.Criar(1).Value.EhMenorQue(VersaoAplicacaoValueObject.Criar(2).Value).Should().BeTrue();
            VersaoAplicacaoValueObject.Criar(2).Value.EhMenorQue(VersaoAplicacaoValueObject.Criar(2).Value).Should().BeFalse();
        }

        [Theory, DisplayName("Deve devolver falha versao.tag-invalida quando a tag for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void DaTag_Invalida_DeveDevolverFalha(string tag)
        {
            var resultado = VersaoAplicacaoValueObject.DaTag(tag);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "versao.tag-invalida" && e.Type == TipoErro.Validation);
        }

        [Theory, DisplayName("Deve devolver falha versao.invalida quando o texto da versão for inválido")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void Criar_TextoInvalido_DeveDevolverFalha(string texto)
        {
            var resultado = VersaoAplicacaoValueObject.Criar(texto);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("versao.invalida");
        }

        [Fact, DisplayName("Deve devolver falha versao.invalida quando a versão for negativa")]
        public void Criar_Negativa_DeveDevolverFalha()
        {
            var resultado = VersaoAplicacaoValueObject.Criar(-1);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("versao.invalida");
        }
    }
}
