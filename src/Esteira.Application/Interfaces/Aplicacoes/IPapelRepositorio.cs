using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Escrita dos papéis da Aplicação (EsteiraContext).
    public interface IPapelRepositorio
    {
        Task<bool> ExisteAsync(long aplicacaoId, string usuario, PapelAplicacao papel, CancellationToken cancellationToken = default);

        // Papel da Aplicação indicada; o de outra Aplicação não conta (404 papel.nao-encontrado).
        Task<PapelAplicacaoEntity?> ObterAsync(long aplicacaoId, Guid publicId, CancellationToken cancellationToken = default);

        Task AdicionarAsync(PapelAplicacaoEntity papel, CancellationToken cancellationToken = default);

        void Remover(PapelAplicacaoEntity papel);
    }
}
