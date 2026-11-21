using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class AplicacaoRepositorio(EsteiraContext contexto) : IAplicacaoRepositorio
    {
        public Task<AplicacaoEntity?> ObterAsync(long id, CancellationToken cancellationToken = default)
            => contexto.Set<AplicacaoEntity>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public Task<bool> SiglaExisteAsync(string sigla, long? exceto, CancellationToken cancellationToken = default)
            => contexto.Set<AplicacaoEntity>().AnyAsync(a => a.Sigla == sigla && (exceto == null || a.Id != exceto), cancellationToken);

        public Task<bool> GrupoVinculadoAsync(long grupoGitLabId, CancellationToken cancellationToken = default)
            => contexto.Set<AplicacaoEntity>().AnyAsync(a => a.GrupoGitLabId == grupoGitLabId, cancellationToken);

        public async Task AdicionarAsync(AplicacaoEntity aplicacao, CancellationToken cancellationToken = default)
            => await contexto.Set<AplicacaoEntity>().AddAsync(aplicacao, cancellationToken);

        public async Task AdicionarPoliticaAsync(PoliticaSeveridadeEntity politica, CancellationToken cancellationToken = default)
            => await contexto.Set<PoliticaSeveridadeEntity>().AddAsync(politica, cancellationToken);

        public Task<PoliticaSeveridadeEntity?> ObterPoliticaAsync(long aplicacaoId, AmbientePolitica ambiente, CancellationToken cancellationToken = default)
            => contexto.Set<PoliticaSeveridadeEntity>().FirstOrDefaultAsync(p => p.AplicacaoId == aplicacaoId && p.Ambiente == ambiente, cancellationToken);
    }
}
