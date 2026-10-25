namespace Esteira.Api.Paginacao
{
    // Nomes dos headers da ADR-0002 num lugar só: o leitor, o filtro de resultado e o CORS usam estas constantes.
    public static class HeadersPaginacao
    {
        public const string Pagina = "X-Page";
        public const string TamanhoPagina = "X-Page-Size";
        public const string Total = "X-Total-Count";
        public const string TotalPaginas = "X-Total-Pages";

        public static readonly string[] Resposta = [Total, TotalPaginas, Pagina, TamanhoPagina];
    }
}
