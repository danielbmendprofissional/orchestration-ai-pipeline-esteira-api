using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
namespace Esteira.Domain.FluxosDeploy.Interfaces
{
    public interface IFluxoDeployRepository: IRepositoryBase<FluxoDeployEntity>
    {
        Task<FluxoDeployEntity> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FluxoDeployEntity>> ObterPorSistemaSomenteEtapasAsync(string sistema, StatusEnum status, CancellationToken cancellationToken = default);
        Task<string> ObterUltimaVersaoSistema(string sistema, CancellationToken cancellationToken = default);
    }
}
