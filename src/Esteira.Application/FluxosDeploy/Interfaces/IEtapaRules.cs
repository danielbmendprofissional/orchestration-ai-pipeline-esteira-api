using Esteira.Domain.Common.Enums;
using Esteira.Domain.FluxosDeploy.Entities;
namespace Esteira.Application.FluxosDeploy.Interfaces
{
    public interface IEtapaRules
    {
        EtapaEnum Etapa { get; }
        Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default);
    }
}
