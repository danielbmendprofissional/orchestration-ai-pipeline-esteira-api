using Esteira.Application.Interfaces.Fatias;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Fatias;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Fatias
{
    public sealed class AcaoBotRepositorio(EsteiraContext contexto) : IAcaoBotRepositorio
    {
        public Task<AcaoBotEntity?> ObterAsync(long id, CancellationToken cancellationToken = default)
            => contexto.Set<AcaoBotEntity>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public Task<AcaoBotEntity?> ObterPorChaveAsync(string chaveIdempotencia, CancellationToken cancellationToken = default)
            => contexto.Set<AcaoBotEntity>().FirstOrDefaultAsync(a => a.ChaveIdempotencia == chaveIdempotencia, cancellationToken);

        public async Task AdicionarAsync(AcaoBotEntity acao, CancellationToken cancellationToken = default)
            => await contexto.Set<AcaoBotEntity>().AddAsync(acao, cancellationToken);
    }
}
