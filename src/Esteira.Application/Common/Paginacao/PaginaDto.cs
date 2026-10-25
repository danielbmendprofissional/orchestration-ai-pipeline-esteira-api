namespace Esteira.Application.Common.Paginacao
{
    // Saída da paginação: a Api escreve Itens no corpo e o resto nos headers X-* (ADR-0002).
    public sealed class PaginaDto<T>
    {
        public required IReadOnlyList<T> Itens { get; init; }
        public required int Pagina { get; init; }
        public required int TamanhoPagina { get; init; }
        public required int Total { get; init; }
        public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
    }
}
