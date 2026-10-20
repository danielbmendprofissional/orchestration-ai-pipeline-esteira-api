namespace Esteira.Application.Common.Paginacao
{
    public interface IRespostaPaginada
    {
        object Resultados { get; }
        PaginacaoDto Pagination { get; }
    }
}
