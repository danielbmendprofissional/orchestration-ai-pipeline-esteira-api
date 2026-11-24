using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    public interface IPessoaRepositorio
    {
        Task<PessoaEntity?> ObterPorSubAsync(string sub, CancellationToken cancellationToken = default);

        // Pelo Id interno: o executor da P2b resolve o nome de quem pediu a Ação (PedidaPorPessoaId).
        Task<PessoaEntity?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);

        Task AdicionarAsync(PessoaEntity pessoa, CancellationToken cancellationToken = default);
    }
}
