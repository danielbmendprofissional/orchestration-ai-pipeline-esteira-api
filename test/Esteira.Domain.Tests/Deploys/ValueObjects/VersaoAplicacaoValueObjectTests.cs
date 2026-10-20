using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Deploys.ValueObjects
{
    public class VersaoAplicacaoValueObjectTests
    {
        [Theory, DisplayName("Deve extrair a versão major da tag")]
        [InlineData("v2.3.1", 2)]
        [InlineData("V10.0", 10)]
        [InlineData("3", 3)]
        public void DaTag_DeveExtrairMajor(string tag, int esperado)
        {
            VersaoAplicacaoValueObject.DaTag(tag).Major.Should().Be(esperado);
        }

        [Fact, DisplayName("Deve aceitar somente tags da mesma versão major")]
        public void AceitaTag_DeveCompararMajor()
        {
            var versao = VersaoAplicacaoValueObject.Criar("v2");

            versao.AceitaTag("v2.9.9").Should().BeTrue();
            versao.AceitaTag("v3.0.0").Should().BeFalse();
        }

        [Fact, DisplayName("Deve comparar as versões major")]
        public void EhMenorQue_DeveCompararMajor()
        {
            VersaoAplicacaoValueObject.Criar(1).EhMenorQue(VersaoAplicacaoValueObject.Criar(2)).Should().BeTrue();
            VersaoAplicacaoValueObject.Criar(2).EhMenorQue(VersaoAplicacaoValueObject.Criar(2)).Should().BeFalse();
        }

        [Theory, DisplayName("Deve lançar exceção quando a tag for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void DaTag_Invalida_DeveLancarDomainException(string tag)
        {
            var act = () => VersaoAplicacaoValueObject.DaTag(tag);

            act.Should().Throw<DomainException>();
        }
    }
}
