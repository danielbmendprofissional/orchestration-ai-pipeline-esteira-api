using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.DTOs.Response;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Specifications;
using Microsoft.EntityFrameworkCore;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Data.Deploys.Repositories
{
    public sealed class DeployQueryRepository : IDeployQueryRepository
    {
        private readonly EsteiraReadContext _context;

        public DeployQueryRepository(EsteiraReadContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ArtefatoDto>> ListarArtefatosAsync(int idDeploy, PaginacaoDto pagination, CancellationToken cancellationToken = default)
            => await _context.Artefatos
                .Aplicar(new ArtefatosPorDeploySpec(idDeploy))
                .PaginarAsync(
                    pagination,
                    a => new ArtefatoDto
                    {
                        Id = a.Id,
                        IdAnalise = a.IdAnalise,
                        Severidade = a.Severidade,
                        TituloVulnerabilidade = a.TituloVulnerabilidade,
                        LocalArquivo = a.LocalArquivo,
                        Ferramenta = a.Analise.Ferramenta
                    },
                    cancellationToken);

        public async Task<ArtefatoCompletoDto> ObterArtefatoAsync(int idArtefato, CancellationToken cancellationToken = default)
            => await _context.Artefatos
                .Where(a => a.Id == idArtefato)
                .Select(a => new ArtefatoCompletoDto
                {
                    Id = a.Id,
                    IdAnalise = a.IdAnalise,
                    Severidade = a.Severidade,
                    TituloVulnerabilidade = a.TituloVulnerabilidade,
                    LocalArquivo = a.LocalArquivo,
                    Ferramenta = a.Analise.Ferramenta,
                    Referencias = a.Referencias,
                    DescricaoVulnerabilidade = a.DescricaoVulnerabilidade,
                    CatalogoCWE = a.CatalogoCWE,
                    CatalogoOWASP = a.CatalogoOWASP,
                    CatalogoCVE = a.CatalogoCVE,
                    PacoteCorrecao = a.PacoteCorrecao,
                    Linha = a.Linha
                })
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<AnaliseEmExecucaoDto>> ListarAnalisesDinamicasEmExecucaoAsync(CancellationToken cancellationToken = default)
            => await _context.Analises
                .Aplicar(new AnalisesDinamicasEmExecucaoSpec())
                .Select(a => new AnaliseEmExecucaoDto
                {
                    IdAnalise = a.Id,
                    ScanId = a.ScanId
                })
                .ToListAsync(cancellationToken);
    }
}
