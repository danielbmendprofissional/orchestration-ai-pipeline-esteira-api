using Esteira.Domain.Fatias;

namespace Esteira.Application.Interfaces.Fatias
{
    // Escrita das Ações do bot (EsteiraContext). A chave de idempotência é única no banco.
    public interface IAcaoBotRepositorio
    {
        Task<AcaoBotEntity?> ObterAsync(long id, CancellationToken cancellationToken = default);

        // Pelo PublicId que o executor recebe na mensagem AcaoBotPedida.
        Task<AcaoBotEntity?> ObterPorPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        Task<AcaoBotEntity?> ObterPorChaveAsync(string chaveIdempotencia, CancellationToken cancellationToken = default);

        // Todas as Ações da Fatia na Aplicação (qualquer tipo e estado), com tracking: base da tentativa e das regras de recusa.
        Task<IReadOnlyList<AcaoBotEntity>> ListarDaFatiaAsync(long aplicacaoId, string fatiaCodigo, CancellationToken cancellationToken = default);

        // Trava de transação (pg_advisory_xact_lock) por Aplicação+Fatia: serializa os pedidos de Ação da mesma Fatia.
        // Só vale dentro de uma transação aberta; solta no commit/rollback. Chamar antes de reler as Ações da Fatia.
        Task TravarFatiaAsync(long aplicacaoId, string fatiaCodigo, CancellationToken cancellationToken = default);

        Task AdicionarAsync(AcaoBotEntity acao, CancellationToken cancellationToken = default);
    }
}
