using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Porta de leitura do acesso: resolve PublicId -> Id uma vez e lista os papéis da pessoa na Aplicação.
    public interface IAcessoAplicacaoQuery
    {
        Task<long?> ResolverIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PapelAplicacao>> PapeisAsync(long aplicacaoId, string sub, CancellationToken cancellationToken = default);
    }
}
