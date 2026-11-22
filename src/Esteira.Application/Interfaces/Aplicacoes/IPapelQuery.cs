using Esteira.Application.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Leitura dos papéis da Aplicação (EsteiraReadContext), já com nome e e-mail da Pessoa quando existir.
    public interface IPapelQuery
    {
        Task<IReadOnlyList<PapelResumoDto>> ListarAsync(long aplicacaoId, CancellationToken cancellationToken = default);
    }
}
