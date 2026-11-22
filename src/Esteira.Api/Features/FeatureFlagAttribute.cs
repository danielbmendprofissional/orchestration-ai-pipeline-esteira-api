using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Esteira.Api.Features
{
    // Feature flag por Fatia (padrao-api): lê `Features:<Fatia>` da configuração; só o valor `true` liga.
    // Ausente, vazio ou qualquer outro valor = desligado -> 404 sem corpo. É filtro de recurso para valer antes
    // de binding e validação. Uso: [FeatureFlag("A1")] no controller ou na action.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FeatureFlagAttribute(string fatia) : Attribute, IResourceFilter
    {
        public string Fatia { get; } = fatia;

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var configuracao = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            if (!bool.TryParse(configuracao[$"Features:{Fatia}"], out var ligado) || !ligado)
                context.Result = new NotFoundResult();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }
}
