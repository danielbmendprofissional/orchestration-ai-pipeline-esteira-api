using System.ComponentModel;
using AwesomeAssertions;
using Esteira.IoC;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Esteira.Integration.Tests.Fatias
{
    [Trait("Fatia", "P2b")]
    public class OpcoesDeRevisaoBotTests
    {
        private static OpcoesDeRevisao Com(string? valor)
            => new(new ConfigurationBuilder()
                .AddInMemoryCollection(valor is null ? [] : new Dictionary<string, string?> { ["Bot:TentativasMaximas"] = valor })
                .Build());

        [Theory, DisplayName("9: Bot:TentativasMaximas tem padrão 3 e só aceita inteiro positivo")]
        [InlineData(null, 3)]
        [InlineData("5", 5)]
        [InlineData("0", 3)]
        [InlineData("x", 3)]
        public void Tentativas_maximas(string? valor, int esperado)
            => Com(valor).TentativasMaximas.Should().Be(esperado);
    }
}
