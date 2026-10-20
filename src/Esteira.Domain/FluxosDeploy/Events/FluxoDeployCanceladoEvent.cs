using Esteira.Domain.Common.Events;
namespace Esteira.Domain.FluxosDeploy.Events
{
    public sealed record FluxoDeployCanceladoEvent(int IdFluxoDeploy, DateTime OcorridoEm) : IDomainEvent;
}
