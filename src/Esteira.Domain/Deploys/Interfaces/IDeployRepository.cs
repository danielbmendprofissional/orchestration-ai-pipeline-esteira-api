using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Interfaces;
using Esteira.Domain.Deploys.Entities;
namespace Esteira.Domain.Deploys.Interfaces
{
    public interface IDeployRepository: IRepositoryBase<DeployEntity>
    {
        Task<DeployEntity> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DeployEntity> ObterPorAnaliseAsync(int idAnalise, CancellationToken cancellationToken = default);
        Task<bool> ExisteAnaliseDinamicaEmExecucaoNaUrlAsync(string urlAlvo, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DeployEntity>> ObterPorFluxosAsync(IEnumerable<int> idsFluxoDeploy, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DeployEntity>> ObterPorFluxoEStatusAsync(int idFluxoDeploy, StatusEnum status, CancellationToken cancellationToken = default);
    }
}
