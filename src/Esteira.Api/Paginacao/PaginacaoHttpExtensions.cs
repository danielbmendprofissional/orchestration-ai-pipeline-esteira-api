using Esteira.Application.Common.Paginacao;
using Newtonsoft.Json;

namespace Esteira.Api.Paginacao
{
    public static class PaginacaoHttpExtensions
    {
        public const string Header = "Pagination";

        public static PaginacaoDto ObterPaginacao(this HttpContext context)
        {
            var valor = context.Request.Headers[Header].ToString();

            if (string.IsNullOrWhiteSpace(valor))
                return new PaginacaoDto();

            try
            {
                return JsonConvert.DeserializeObject<PaginacaoDto>(valor) ?? new PaginacaoDto();
            }
            catch (JsonException)
            {
                return new PaginacaoDto();
            }
        }
    }
}
