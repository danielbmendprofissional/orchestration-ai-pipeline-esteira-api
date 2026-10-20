using Newtonsoft.Json;

namespace Esteira.Application.Common.Paginacao
{
    public class PaginacaoDto
    {
        public const int TamanhoPadrao = 10;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = TamanhoPadrao;
        public string Order { get; set; }
        public string SortOrder { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

        [JsonIgnore]
        public int Pular => (Math.Max(Page, 1) - 1) * Tamanho;
        [JsonIgnore]
        public int Tamanho => PageSize <= 0 ? TamanhoPadrao : PageSize;
    }
}
