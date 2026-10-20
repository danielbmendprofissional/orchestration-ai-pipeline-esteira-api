using Esteira.Domain.Common.Enums;
namespace Esteira.Application.FluxosDeploy.Interfaces
{
    public sealed record FiltroFluxoDeploy(
        string Sistema = null,
        DateOnly? DataInicio = null,
        DateOnly? DataFim = null,
        StatusEnum? Status = null);
}
