using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Porta de leitura do acesso: resolve PublicId -> Id uma vez e lista os papéis da pessoa na Aplicação.
    public interface IAcessoAplicacaoQuery
    {
        Task<long?> ResolverIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        // O inverso de ResolverIdAsync (a chave do instantâneo do Painel é o PublicId).
        Task<Guid?> ObterPublicIdAsync(long aplicacaoId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PapelAplicacao>> PapeisAsync(long aplicacaoId, string sub, CancellationToken cancellationToken = default);
    }
}
