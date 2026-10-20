using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Data.Common.Specifications;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Esteira.Data.Deploys.Specifications;
using Esteira.Data.FluxosDeploy.Specifications;
using Esteira.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Data.FluxosDeploy.Repositories
{
    public sealed class FluxoDeployQueryRepository : IFluxoDeployQueryRepository
    {
        private readonly EsteiraReadContext _context;

        public FluxoDeployQueryRepository(EsteiraReadContext context)
        {
            _context = context;
        }

        public Task<bool> ExisteAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => _context.FluxosDeploy.ExisteAsync(new FluxoDeployPorIdSpec(idFluxoDeploy), cancellationToken);

        public async Task<string> ObterCriadorAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeploy
                .Aplicar(new FluxoDeployPorIdSpec(idFluxoDeploy))
                .Select(a => a.Usuario)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<StatusEnum?> ObterStatusAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeploy
                .Where(a => a.Id == idFluxoDeploy)
                .Select(a => (StatusEnum?)a.Status)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<FluxoDeployDto>> ListarAsync(FiltroFluxoDeploy filtro, PaginacaoDto pagination, CancellationToken cancellationToken = default)
            => await _context.FluxosDeploy
                .Aplicar(new FluxosDeployPorFiltroSpec(filtro))
                .PaginarAsync(
                    pagination,
                    a => new FluxoDeployDto
                    {
                        Id = a.Id,
                        Status = a.Status.ToString(),
                        Inicio = a.Inicio,
                        Fim = a.Fim,
                        Sistema = a.Sistema,
                        Versao = a.Versao,
                        Usuario = a.Usuario
                    },
                    cancellationToken);

        public async Task<FluxoDeployDto> ObterComEtapasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
        {
            var fluxo = (await _context.FluxosDeploy
                .Aplicar(new FluxoDeployComEtapasSpec(idFluxoDeploy))
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

            if (fluxo is null)
                return null;

            var etapas = fluxo.Etapas
                .OrderBy(e => e.Ordem)
                .Select(e => new EtapasFluxoDeployDto
                {
                    SglEtapa = e.SglEtapa,
                    Ordem = e.Ordem,
                    Status = e.Status.ToString(),
                    Descricao = e.Etapa?.Descricao
                })
                .ToList();

            return new FluxoDeployDto
            {
                Id = fluxo.Id,
                Status = fluxo.Status.ToString(),
                Inicio = fluxo.Inicio,
                Fim = fluxo.Fim,
                Sistema = fluxo.Sistema,
                Versao = fluxo.Versao,
                Usuario = fluxo.Usuario,
                Etapas = etapas.Count == 0 ? null : etapas
            };
        }

        public async Task<FluxoDeployCompletoDto> ObterDetalheBranchAsync(int idFluxoDeploy, string branch, CancellationToken cancellationToken = default)
        {
            if (!await ExisteAsync(idFluxoDeploy, cancellationToken))
                return null;

            var deploys = await _context.Deploys
                .Aplicar(new DeploysPorFluxoEBranchSpec(idFluxoDeploy, branch))
                .ToListAsync(cancellationToken);

            var analises = deploys.SelectMany(d => d.Analises).ToList();

            return new FluxoDeployCompletoDto
            {
                Id = idFluxoDeploy,
                Branch = deploys.FirstOrDefault()?.Branch ?? string.Empty,
                Vulnerabilidades = new VulnerabilidadeDto
                {
                    Critica = analises.Sum(a => a.VulnerabilidadeCritica),
                    Alta = analises.Sum(a => a.VulnerabilidadeAlta),
                    Media = analises.Sum(a => a.VulnerabilidadeMedia),
                    Baixa = analises.Sum(a => a.VulnerabilidadeBaixa)
                },
                Deploys = deploys.Select(ParaDeployDto).ToList()
            };
        }

        public async Task<FluxoDeployResumoDto> ObterResumoAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeployResumo
                .Where(a => a.IdFluxoDeploy == idFluxoDeploy)
                .Select(a => new FluxoDeployResumoDto
                {
                    IdFluxoDeploy = a.IdFluxoDeploy,
                    Status = a.Status.ToString(),
                    Inicio = a.Inicio,
                    Fim = a.Fim,
                    UsuarioCriacao = a.UsuarioCriacao,
                    Sistema = a.Sistema,
                    Versao = a.Versao,
                    Linguagens = a.Linguagens,
                    ProjetosGit = a.ProjetosGit,
                    VulnerabilidadeCritica = a.VulnerabilidadeCritica,
                    VulnerabilidadeAlta = a.VulnerabilidadeAlta,
                    VulnerabilidadeMedia = a.VulnerabilidadeMedia,
                    VulnerabilidadeBaixa = a.VulnerabilidadeBaixa,
                    TarefasNegocio = a.TarefasNegocio,
                    AprovacaoTecnica = a.AprovacaoTecnica,
                    Crs = a.Crs,
                    Etapas = a.Etapas,
                    Gmuds = a.Gmuds,
                    VersoesTarefas = a.VersoesTarefas
                })
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<BdCrsDto>> ListarCrsAsync(int idFluxoDeploy, PaginacaoDto pagination, CancellationToken cancellationToken = default)
            => await _context.FluxosDeployCrs
                .Aplicar(new CrsPorFluxoDeploySpec(idFluxoDeploy))
                .PaginarAsync(
                    pagination,
                    a => new BdCrsDto
                    {
                        IdCrs = a.Id.ToString(),
                        Crs = a.Crs,
                        Url = a.Url
                    },
                    cancellationToken);

        public async Task<GmudDto> ObterGmudAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeployGmuds
                .Where(a => a.IdFluxoDeploy == idFluxoDeploy)
                .Select(a => new GmudDto
                {
                    Url = a.Url,
                    Justificativa = a.Justificativa,
                    Usuario = a.Usuario,
                    Execucao = a.Execucao
                })
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<VersaoTarefasDto> ObterVersaoTarefasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeployVersoesTarefas
                .Where(a => a.IdFluxoDeploy == idFluxoDeploy)
                .Select(a => new VersaoTarefasDto
                {
                    IdProjeto = a.IdProjeto,
                    IdVersao = a.IdVersao
                })
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<int>> ObterTarefasNegocioAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
            => await _context.FluxosDeployAprovadoresNegocio
                .Where(a => a.IdFluxoDeploy == idFluxoDeploy)
                .Select(a => a.IdTarefa)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<AprovacaoTecnicaDto>> ObterAprovacoesTecnicasAsync(int idFluxoDeploy, CancellationToken cancellationToken = default)
        {
            var aprovadores = await _context.AprovadoresTecnicos
                .Where(a => a.Status == StatusEnum.ATIVO)
                .ToListAsync(cancellationToken);

            var aprovacoes = await _context.FluxosDeployAprovadoresTecnicos
                .Aplicar(new AprovacoesTecnicasPorFluxoDeploySpec(idFluxoDeploy))
                .ToListAsync(cancellationToken);

            return aprovadores
                .GroupBy(a => a.Equipe)
                .Select(equipe =>
                {
                    var aprovacao = aprovacoes.FirstOrDefault(a => a.AprovadorTecnico.Equipe == equipe.Key);

                    return new AprovacaoTecnicaDto
                    {
                        Usuario = aprovacao?.AprovadorTecnico.Usuario ?? $"Ainda não houve aprovação da equipe {equipe.Key}",
                        Execucao = aprovacao?.Execucao,
                        Aprovado = aprovacao is { Aprovado: true } ? 'S' : 'N',
                        Justificativa = aprovacao?.Justificativa,
                        Equipe = equipe.Key,
                        Integrantes = aprovacao is null
                            ? equipe.Select(b => new EquipeDto { Id = b.Id, Usuario = b.Usuario }).ToList()
                            : null
                    };
                })
                .ToList();
        }

        private static DeployDto ParaDeployDto(DeployQueryModel deploy)
            => new()
            {
                IdDeploy = deploy.Id,
                IdProjetoGit = deploy.IdProjetoGit,
                NomeProjetoGit = deploy.NomeProjetoGit,
                Linguagem = deploy.Linguagem,
                VersaoAplicacao = deploy.VersaoAplicacao,
                Status = deploy.Status.GetDescription(),
                Analises = deploy.Analises.Select(a => new AnaliseDto
                {
                    IdAnalise = a.Id,
                    Ferramenta = a.Ferramenta,
                    TipoAnalise = a.TipoAnalise,
                    IdPipeline = a.IdPipeline,
                    Tag = a.Tag,
                    Status = a.Status.GetDescription(),
                    Vulnerabilidades = new VulnerabilidadeDto
                    {
                        Critica = a.VulnerabilidadeCritica,
                        Alta = a.VulnerabilidadeAlta,
                        Media = a.VulnerabilidadeMedia,
                        Baixa = a.VulnerabilidadeBaixa
                    }
                }).ToList()
            };
    }
}
