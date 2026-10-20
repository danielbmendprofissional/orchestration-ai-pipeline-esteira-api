using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.ConfiguracoesSistema.Entities
{
    public class ConfiguracaoSistemaEntityTests
    {
        [Fact, DisplayName("Deve criar sem limites quando nenhum for informado")]
        public void Criar_SemLimites_DeveUsarNenhum()
        {
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA");

            config.Sistema.Should().Be("SISTEMA");
            config.Limites.Should().Be(LimitesSeveridadeValueObject.Nenhum);
        }

        [Theory, DisplayName("Deve lançar exceção quando o sistema for vazio")]
        [InlineData(null)]
        [InlineData("")]
        public void Criar_SistemaVazio_DeveLancarDomainException(string sistema)
        {
            var act = () => ConfiguracaoSistemaEntity.Criar(sistema);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve alterar os limites")]
        public void AlterarLimites_DeveAtualizar()
        {
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA");
            var limites = new LimitesSeveridadeValueObject(1, 2, 3, 4);

            config.AlterarLimites(limites);

            config.Limites.Should().Be(limites);
        }

        [Fact, DisplayName("Deve lançar exceção ao alterar para limites nulos")]
        public void AlterarLimites_Nulo_DeveLancarDomainException()
        {
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA");

            var act = () => config.AlterarLimites(null);

            act.Should().Throw<DomainException>();
        }
    }
}
