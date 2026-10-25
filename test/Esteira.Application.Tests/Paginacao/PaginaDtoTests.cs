using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Application.Tests.Paginacao
{
    public class PaginaDtoTests
    {
        private static PaginaDto<int> Pagina(int total, int tamanho) => new()
        {
            Itens = [],
            Pagina = 1,
            TamanhoPagina = tamanho,
            Total = total
        };

        [Fact, DisplayName("Deve calcular zero páginas quando o total é zero")]
        public void TotalZero_DeveTerZeroPaginas() => Pagina(0, 10).TotalPaginas.Should().Be(0);

        [Fact, DisplayName("Deve calcular o número exato de páginas quando o total é múltiplo do tamanho")]
        public void TotalExato_DeveDividirSemResto() => Pagina(30, 10).TotalPaginas.Should().Be(3);

        [Fact, DisplayName("Deve arredondar para cima quando o total tem resto")]
        public void TotalComResto_DeveArredondarParaCima() => Pagina(31, 10).TotalPaginas.Should().Be(4);
    }
}
