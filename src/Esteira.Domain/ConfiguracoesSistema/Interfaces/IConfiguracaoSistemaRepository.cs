using Esteira.Domain.Common.Interfaces;
using Esteira.Domain.ConfiguracoesSistema.Entities;
namespace Esteira.Domain.ConfiguracoesSistema.Interfaces
{
    public interface IConfiguracaoSistemaRepository : IRepositoryBase<ConfiguracaoSistemaEntity>
    {
        Task<ConfiguracaoSistemaEntity> GetBySistemaAsync(string sistema, CancellationToken cancellationToken = default);
        Task<bool> ExisteAsync(string sistema, CancellationToken cancellationToken = default);
    }
}
