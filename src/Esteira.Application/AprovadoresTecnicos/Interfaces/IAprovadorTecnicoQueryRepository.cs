using Esteira.Application.DTOs.Response;
namespace Esteira.Application.AprovadoresTecnicos.Interfaces
{
    public interface IAprovadorTecnicoQueryRepository
    {
        Task<IReadOnlyList<EquipeDto>> ListarIntegrantesAtivosAsync(string equipe, CancellationToken cancellationToken = default);
    }
}
