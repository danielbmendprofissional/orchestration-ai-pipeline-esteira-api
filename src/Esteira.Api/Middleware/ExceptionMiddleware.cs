using FluentValidation;
using Esteira.Domain.Common.Exceptions;

namespace Esteira.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var mensagem = exception is ValidationException validationEx
                ? string.Join(" ", validationEx.Errors.Select(e => e.ErrorMessage).Distinct())
                : exception.Message;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = exception switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                DomainException domainEx => domainEx.StatusCode ?? StatusCodes.Status400BadRequest,
                IntegrationException integrationEx => integrationEx.StatusCode ?? StatusCodes.Status500InternalServerError,
                NotFoundException notFoundEx => notFoundEx.StatusCode ?? StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "{Metodo} {Rota} falhou: {Mensagem}", context.Request.Method, context.Request.Path, mensagem);
            else
                _logger.LogWarning("{Metodo} {Rota} recusada ({Status}): {Mensagem}", context.Request.Method, context.Request.Path, context.Response.StatusCode, mensagem);

            await context.Response.WriteAsJsonAsync(new { message = mensagem });
        }
    }
}
