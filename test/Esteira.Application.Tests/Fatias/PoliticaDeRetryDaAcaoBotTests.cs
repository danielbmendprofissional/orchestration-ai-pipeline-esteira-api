using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Messaging.Consumers;
using Xunit;

namespace Esteira.Application.Tests.Fatias
{
    // P2b.4, critério 9: a política de retry do consumer (limite vindo de Bot:TentativasMaximas, espera crescente).
    [Trait("Fatia", "P2b")]
    public class PoliticaDeRetryDaAcaoBotTests
    {
        [Theory, DisplayName("9: o limite de reentregas é TentativasMaximas - 1 (nunca negativo)")]
        [InlineData(3, 2)]
        [InlineData(1, 0)]
        [InlineData(0, 0)]
        public void Reentregas(int maximas, int esperado)
            => PoliticaDeRetryDaAcaoBot.Reentregas(maximas).Should().Be(esperado);

        [Fact, DisplayName("9: a espera cresce a cada reentrega")]
        public void Espera_cresce()
        {
            var e1 = PoliticaDeRetryDaAcaoBot.Espera(1);
            var e2 = PoliticaDeRetryDaAcaoBot.Espera(2);
            var e3 = PoliticaDeRetryDaAcaoBot.Espera(3);

            e1.Should().BeGreaterThan(TimeSpan.Zero);
            e2.Should().BeGreaterThan(e1);
            e3.Should().BeGreaterThan(e2);
        }
    }
}
