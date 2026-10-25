using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Erros
{
    // ADR-0003: só o inesperado vira Problem Details (RFC 9457). O corpo nunca leva a mensagem nem o
    // stacktrace da exception, em nenhum ambiente; quem investiga acha a exception no log pelo traceId.
    public sealed class ErroInesperadoHandler(
        IProblemDetailsService problemDetails,
        ILogger<ErroInesperadoHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            logger.LogError(exception, "{Metodo} {Rota} falhou com erro inesperado (traceId {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, traceId);

            var problema = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Erro inesperado",
                Status = StatusCodes.Status500InternalServerError
            };
            problema.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var escrito = await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problema,
                Exception = exception
            });

            // Cliente que não aceita JSON (Accept: text/html) ainda recebe o mesmo corpo, nunca a página de erro.
            if (!escrito)
                await httpContext.Response.WriteAsJsonAsync(problema, options: null, "application/problem+json", cancellationToken);

            return true;
        }
    }
}
