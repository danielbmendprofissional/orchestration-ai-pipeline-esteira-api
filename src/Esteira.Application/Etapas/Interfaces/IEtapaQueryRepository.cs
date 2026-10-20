using Esteira.Application.DTOs.Response;
namespace Esteira.Application.Etapas.Interfaces
{
    public interface IEtapaQueryRepository
    {
        Task<IReadOnlyList<EtapaDto>> ListarAtivasAsync(CancellationToken cancellationToken = default);
    }
}
