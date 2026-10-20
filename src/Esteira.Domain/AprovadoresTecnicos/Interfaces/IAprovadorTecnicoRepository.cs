using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Interfaces;
namespace Esteira.Domain.AprovadoresTecnicos.Interfaces
{
    public interface IAprovadorTecnicoRepository : IRepositoryBase<AprovadorTecnicoEntity>
    {
        Task<AprovadorTecnicoEntity> GetByUsuarioAsync(string usuario, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AprovadorTecnicoEntity>> ObterPorIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AprovadorTecnicoEntity>> ObterPorEquipeAsync(string equipe, CancellationToken cancellationToken = default);
        Task<List<string>> ObterEquipesTecnicasAsync(CancellationToken cancellationToken = default);
    }
}
