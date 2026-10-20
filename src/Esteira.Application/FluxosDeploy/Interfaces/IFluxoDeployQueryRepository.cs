using Esteira.Application.Common.Paginacao;
using Esteira.Application.DTOs.Response;
using Esteira.Domain.Common.Enums;
namespace Esteira.Application.FluxosDeploy.Interfaces
{
    public interface IFluxoDeployQueryRepository
    {
        Task<bool> ExisteAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<string> ObterCriadorAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<StatusEnum?> ObterStatusAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FluxoDeployDto>> ListarAsync(FiltroFluxoDeploy filtro, PaginacaoDto pagination, CancellationToken cancellationToken = default);
        Task<FluxoDeployDto> ObterComEtapasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<FluxoDeployCompletoDto> ObterDetalheBranchAsync(int idFluxoDeploy, string branch, CancellationToken cancellationToken = default);
        Task<FluxoDeployResumoDto> ObterResumoAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BdCrsDto>> ListarCrsAsync(int idFluxoDeploy, PaginacaoDto pagination, CancellationToken cancellationToken = default);
        Task<GmudDto> ObterGmudAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<VersaoTarefasDto> ObterVersaoTarefasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<int>> ObterTarefasNegocioAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AprovacaoTecnicaDto>> ObterAprovacoesTecnicasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default);
    }
}
