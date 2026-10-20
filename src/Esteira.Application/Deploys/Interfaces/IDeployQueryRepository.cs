using Esteira.Application.Common.Paginacao;
using Esteira.Application.DTOs.Response;
namespace Esteira.Application.Deploys.Interfaces
{
    public interface IDeployQueryRepository
    {
        Task<IReadOnlyList<ArtefatoDto>> ListarArtefatosAsync(int idDeploy, PaginacaoDto pagination, CancellationToken cancellationToken = default);
        Task<ArtefatoCompletoDto> ObterArtefatoAsync(int idArtefato, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AnaliseEmExecucaoDto>> ListarAnalisesDinamicasEmExecucaoAsync(CancellationToken cancellationToken = default);
    }
}
