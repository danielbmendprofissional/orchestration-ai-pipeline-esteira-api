using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.FluxosDeploy.ValueObjects
{
    public class VersaoFluxoValueObjectTests
    {
        [Fact, DisplayName("Deve iniciar em v1 quando não houver versão anterior")]
        public void Proxima_SemAtual_DeveSerV1()
        {
            VersaoFluxoValueObject.Proxima(null).ToString().Should().Be("v1");
        }

        [Fact, DisplayName("Deve incrementar a versão anterior")]
        public void Proxima_ComAtual_DeveIncrementar()
        {
            VersaoFluxoValueObject.Proxima(VersaoFluxoValueObject.Criar("v9")).ToString().Should().Be("v10");
        }

        [Theory, DisplayName("Deve lançar exceção quando o texto for inválido")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("vx")]
        [InlineData("v0")]
        public void Criar_Invalido_DeveLancarDomainException(string texto)
        {
            var act = () => VersaoFluxoValueObject.Criar(texto);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve ser igual quando o número for o mesmo")]
        public void Equals_MesmoNumero_DeveSerIgual()
        {
            VersaoFluxoValueObject.Criar("v2").Should().Be(VersaoFluxoValueObject.Criar(2));
        }
    }
}
