using AwesomeAssertions;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Tests.Fatias
{
    // P2.5, critério 17: a P2 só registra; a porta do GitLab continua sem nenhum método de escrita (a escrita é do executor da P2b).
    [Trait("Fatia", "P2")]
    public class IGitLabSomenteLeituraTests
    {
        [Fact]
        public void IGitLab_so_tem_metodos_de_leitura()
        {
            var nomes = typeof(IGitLab).GetMethods().Select(m => m.Name).ToList();

            nomes.Should().NotBeEmpty();
            nomes.Should().OnlyContain(n => n.StartsWith("Obter", StringComparison.Ordinal) || n.StartsWith("Listar", StringComparison.Ordinal));
        }
    }
}
