using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Data.Entregas
{
    public sealed class OcupacaoAmbienteRepositorio(EsteiraContext contexto) : IOcupacaoAmbienteRepositorio
    {
        public Task<OcupacaoAmbienteEntity?> ObterAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default)
            => contexto.Set<OcupacaoAmbienteEntity>()
                .FirstOrDefaultAsync(o => o.ProjetoId == projetoId && o.Ambiente == ambiente, cancellationToken);

        public async Task<Result> ReservarAsync(long projetoId, AmbienteDeploy ambiente, long fluxoId, DateTime quando, CancellationToken cancellationToken = default)
        {
            var atual = ambiente == AmbienteDeploy.Qa ? null : await ObterAsync(projetoId, ambiente, cancellationToken);
            var reserva = OcupacaoAmbienteEntity.Reservar(atual, projetoId, ambiente, fluxoId, quando);
            if (!reserva.IsSuccess)
                return Result.Falha([.. reserva.Errors]);

            // Reserva nova (livre quando lido): grava já. Quem perde a corrida bate no índice único.
            if (reserva.Value is { } nova && !ReferenceEquals(nova, atual))
            {
                await contexto.Set<OcupacaoAmbienteEntity>().AddAsync(nova, cancellationToken);
                try
                {
                    await contexto.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // Outro Fluxo reservou entre a leitura e a gravação. Solta a tentativa e decide pela reserva que ganhou.
                    contexto.Entry(nova).State = EntityState.Detached;
                    var vencedora = await contexto.Set<OcupacaoAmbienteEntity>().AsNoTracking()
                        .FirstOrDefaultAsync(o => o.ProjetoId == projetoId && o.Ambiente == ambiente, cancellationToken);
                    return vencedora?.FluxoId == fluxoId ? Result.Sucesso() : Result.Falha(OcupacaoAmbienteEntity.Ocupado());
                }
            }

            return Result.Sucesso();
        }

        public async Task<Result> LiberarAsync(long projetoId, AmbienteDeploy ambiente, long fluxoId, CancellationToken cancellationToken = default)
        {
            var atual = await ObterAsync(projetoId, ambiente, cancellationToken);
            if (atual is null)
                return Result.Sucesso();

            var liberada = atual.Liberar(fluxoId);
            if (liberada.IsSuccess)
                contexto.Set<OcupacaoAmbienteEntity>().Remove(atual);
            return liberada;
        }
    }
}
