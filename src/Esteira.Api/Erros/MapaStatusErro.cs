using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Erros
{
    // ADR-0003: o mapa TipoErro → status mora num lugar só. Qualquer código novo de erro escolhe
    // um destes tipos; se precisar de outro status, muda aqui.
    public static class MapaStatusErro
    {
        private static readonly Dictionary<TipoErro, int> Status = new()
        {
            [TipoErro.Validation] = StatusCodes.Status400BadRequest,
            [TipoErro.Unauthorized] = StatusCodes.Status401Unauthorized,
            [TipoErro.Forbidden] = StatusCodes.Status403Forbidden,
            [TipoErro.NotFound] = StatusCodes.Status404NotFound,
            [TipoErro.Conflict] = StatusCodes.Status409Conflict,
            [TipoErro.Failure] = StatusCodes.Status422UnprocessableEntity
        };

        public static int De(TipoErro tipo) => Status.TryGetValue(tipo, out var status)
            ? status
            : StatusCodes.Status422UnprocessableEntity;
    }
}
