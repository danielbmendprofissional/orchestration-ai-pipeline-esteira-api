using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Repositories;
using Esteira.Data.Common.Specifications;
using Esteira.Data.ConfiguracoesSistema.Specifications;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.ConfiguracoesSistema.Repositories
{
    public sealed class ConfiguracaoSistemaRepository : RepositoryBase<ConfiguracaoSistemaEntity>, IConfiguracaoSistemaRepository
    {
        public ConfiguracaoSistemaRepository(EsteiraContext context) : base(context)
        {
        }

        public async Task<ConfiguracaoSistemaEntity> GetBySistemaAsync(string sistema, CancellationToken cancellationToken = default)
            => await Context.ConfiguracoesSistema.FirstOrDefaultAsync(a => a.Sistema == sistema, cancellationToken);

        public Task<bool> ExisteAsync(string sistema, CancellationToken cancellationToken = default)
            => Context.ConfiguracoesSistema.ExisteAsync(new ConfiguracaoPorSistemaSpec(sistema), cancellationToken);
    }
}
