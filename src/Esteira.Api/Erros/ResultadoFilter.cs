using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Esteira.Api.Erros
{
    // Traduz Result → HTTP num ponto só: sucesso com valor vira 200 com o valor, sucesso sem valor
    // vira 204, falha vira o status do primeiro erro com { "errors": [...] } na ordem original.
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
            resultado.Value = valor;
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
