using Esteira.Domain.Common.Interfaces;
using Esteira.Domain.Etapas.Entities;
namespace Esteira.Domain.Etapas.Interfaces
{
    public interface IEtapaRepository : IRepositoryBase<EtapaEntity>
    {
        Task<EtapaEntity> GetBySiglaAsync(string sigla, CancellationToken cancellationToken = default);
        Task<bool> ExisteSiglaAsync(string sigla, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EtapaEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EtapaEntity>> ObterCatalogoAsync(CancellationToken cancellationToken = default);
        Task<EtapaEntity> ObterUltimaEtapa(CancellationToken cancellationToken = default);
    }
}
