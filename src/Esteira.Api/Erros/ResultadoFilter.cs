using Esteira.Api.Paginacao;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Esteira.Api.Erros
{
    // Traduz Result → HTTP num ponto só: sucesso com valor vira 200 com o valor, sucesso sem valor
    // vira 204, falha vira o status do primeiro erro com { "errors": [...] } na ordem original.
    // Sucesso com PaginaDto<T> (ADR-0002) vira 200 com o array puro de Itens e a página nos headers X-*.
    // Criação (201) continua no controller, que já entrega o valor extraído no ObjectResult.
    public sealed class ResultadoFilter : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ObjectResult resultado || resultado.Value is not Result retorno)
                return;

            if (retorno.IsSuccess)
            {
                TraduzirSucesso(context, resultado, retorno);
                return;
            }

            resultado.StatusCode = MapaStatusErro.De(retorno.Errors[0].Type);
            resultado.Value = RespostaDeErro.De(retorno.Errors);
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }

        private static void TraduzirSucesso(ResultExecutingContext context, ObjectResult resultado, Result retorno)
        {
            var valor = ValorDe(retorno);

            if (valor is null)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status204NoContent);
                return;
            }

            resultado.StatusCode = StatusCodes.Status200OK;
            resultado.Value = EhPagina(valor) ? EscreverPagina(context.HttpContext.Response, valor) : valor;
        }

        private static bool EhPagina(object valor)
        {
            var tipo = valor.GetType();
            return tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(PaginaDto<>);
        }

        // PaginaDto<T> é genérico em T, então as propriedades saem por reflexão, como o valor do Result.
        private static object EscreverPagina(HttpResponse resposta, object pagina)
        {
            string Ler(string nome) => Convert.ToString(pagina.GetType().GetProperty(nome)!.GetValue(pagina), System.Globalization.CultureInfo.InvariantCulture)!;

            resposta.Headers[HeadersPaginacao.Total] = Ler(nameof(PaginaDto<object>.Total));
            resposta.Headers[HeadersPaginacao.TotalPaginas] = Ler(nameof(PaginaDto<object>.TotalPaginas));
            resposta.Headers[HeadersPaginacao.Pagina] = Ler(nameof(PaginaDto<object>.Pagina));
            resposta.Headers[HeadersPaginacao.TamanhoPagina] = Ler(nameof(PaginaDto<object>.TamanhoPagina));

            return pagina.GetType().GetProperty(nameof(PaginaDto<object>.Itens))!.GetValue(pagina)!;
        }

        // Result<T> é genérico em T; o valor só se lê pela instância tipada. Result puro não tem valor.
        private static object? ValorDe(Result retorno)
        {
            var tipo = retorno.GetType();
            if (!tipo.IsGenericType || tipo.GetGenericTypeDefinition() != typeof(Result<>))
                return null;

            return tipo.GetProperty(nameof(Result<object>.Value))!.GetValue(retorno);
        }
    }
}
