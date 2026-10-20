using Esteira.Application.Common.Paginacao;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Esteira.Api.Paginacao
{
    public sealed class PaginacaoResultFilter : IResultFilter
    {
        private static readonly JsonSerializerSettings Configuracao = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ObjectResult { Value: IRespostaPaginada resposta } resultado)
                return;

            context.HttpContext.Response.Headers[PaginacaoHttpExtensions.Header] = JsonConvert.SerializeObject(resposta.Pagination, Configuracao);
            resultado.Value = resposta.Resultados;
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }
}
