using Esteira.Application.Common.Paginacao;

namespace Esteira.Application.DTOs.Response.Common
{
    public class BaseListaResponseDto<T> : IRespostaPaginada
    {
        public IReadOnlyList<T> Results { get; set; }
        public PaginacaoDto Pagination { get; set; }

        object IRespostaPaginada.Resultados => Results;
    }
}
