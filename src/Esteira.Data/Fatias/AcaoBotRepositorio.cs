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

        public Task<AcaoBotEntity?> ObterPorPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
            => contexto.Set<AcaoBotEntity>().FirstOrDefaultAsync(a => a.PublicId == publicId, cancellationToken);

        public Task<AcaoBotEntity?> ObterPorChaveAsync(string chaveIdempotencia, CancellationToken cancellationToken = default)
            => contexto.Set<AcaoBotEntity>().FirstOrDefaultAsync(a => a.ChaveIdempotencia == chaveIdempotencia, cancellationToken);

        public async Task<IReadOnlyList<AcaoBotEntity>> ListarDaFatiaAsync(long aplicacaoId, string fatiaCodigo, CancellationToken cancellationToken = default)
            => await contexto.Set<AcaoBotEntity>().Where(a => a.AplicacaoId == aplicacaoId && a.FatiaCodigo == fatiaCodigo)
                .OrderBy(a => a.Id).ToListAsync(cancellationToken);

        // hashtextextended do Postgres: hash estável de 64 bits, sem depender de GetHashCode do .NET.
        public async Task TravarFatiaAsync(long aplicacaoId, string fatiaCodigo, CancellationToken cancellationToken = default)
        {
            var chave = $"acao-bot:{aplicacaoId}:{fatiaCodigo.ToUpperInvariant()}";
            await contexto.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 0))", cancellationToken);
        }

        public async Task AdicionarAsync(AcaoBotEntity acao, CancellationToken cancellationToken = default)
            => await contexto.Set<AcaoBotEntity>().AddAsync(acao, cancellationToken);
    }
}
