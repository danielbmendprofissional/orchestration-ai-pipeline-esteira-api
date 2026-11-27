using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Entregas
{
    public interface IOcupacaoAmbienteRepositorio
    {
        Task<OcupacaoAmbienteEntity?> ObterAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default);

        // Reserva Projeto×Ambiente para o Fluxo (ADR-0009). Grava na hora (SaveChanges do contexto, o que também
        // persiste o que estiver pendente nele): a invariante é do índice único, e a violação (corrida) vira
        // `ambiente.ocupado`, nunca exceção. Idempotente para o mesmo Fluxo; qa não reserva nada.
        Task<Result> ReservarAsync(long projetoId, AmbienteDeploy ambiente, long fluxoId, DateTime quando, CancellationToken cancellationToken = default);

        // Marca a linha para remoção (grava no SaveChanges do IUnitOfWork). Sem ocupação: sucesso (idempotente).
        // Ocupação de outro Fluxo: `ambiente.ocupado`.
        Task<Result> LiberarAsync(long projetoId, AmbienteDeploy ambiente, long fluxoId, CancellationToken cancellationToken = default);
    }
}
