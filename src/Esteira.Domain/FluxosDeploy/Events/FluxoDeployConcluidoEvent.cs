using Esteira.Domain.Common.Events;
namespace Esteira.Domain.FluxosDeploy.Events
{
    public sealed record FluxoDeployConcluidoEvent(int IdFluxoDeploy, DateTime OcorridoEm) : IDomainEvent;
}
