namespace Esteira.Application.Common.Paginacao
{
    // Entrada da paginação, já validada pela Api (ADR-0002): página e tamanho dentro dos limites,
    // sentido "asc"/"desc". Ordenacao é o nome de uma propriedade do DTO da lista.
    public class PaginacaoDto
    {
        public const int TamanhoPadrao = 10;
        public const int TamanhoMaximo = 100;

        public int Pagina { get; set; } = 1;
        public int TamanhoPagina { get; set; } = TamanhoPadrao;
        public string? Ordenacao { get; set; }
        public string? Sentido { get; set; }
    }
}
