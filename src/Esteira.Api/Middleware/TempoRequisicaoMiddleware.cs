using System.Diagnostics;
using System.Globalization;

namespace Esteira.Api.Middleware
{
    public class TempoRequisicaoMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TempoRequisicaoMiddleware> _logger;

        public TempoRequisicaoMiddleware(RequestDelegate next, ILogger<TempoRequisicaoMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var cronometro = Stopwatch.StartNew();

            context.Response.OnStarting(() =>
            {
                context.Response.Headers["Server-Timing"] = $"app;dur={cronometro.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)}";
                return Task.CompletedTask;
            });

            try
            {
                await _next(context);
            }
            finally
            {
                cronometro.Stop();
                _logger.LogInformation(
                    "{Metodo} {Rota} {Status} {Duracao}ms",
                    context.Request.Method,
                    // Só o caminho: a query do callback de login carrega `code` e `state` (ADR-0004) e não pode ir pro log.
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    cronometro.ElapsedMilliseconds);
            }
        }
    }
}
