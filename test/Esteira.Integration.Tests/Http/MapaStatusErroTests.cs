using AwesomeAssertions;
using Esteira.Api.Erros;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Integration.Tests.Http
{
    // P1.5: `painel.atualizacao-recente` é 429.
    [Trait("Fatia", "P1")]
    public class MapaStatusErroTests
    {
        [Fact]
        public void TooManyRequests_vira_429() => MapaStatusErro.De(TipoErro.TooManyRequests).Should().Be(429);
    }
}
