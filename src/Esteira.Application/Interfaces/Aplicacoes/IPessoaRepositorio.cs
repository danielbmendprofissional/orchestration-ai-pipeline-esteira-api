using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    public interface IPessoaRepositorio
    {
        Task<PessoaEntity?> ObterPorSubAsync(string sub, CancellationToken cancellationToken = default);

        Task AdicionarAsync(PessoaEntity pessoa, CancellationToken cancellationToken = default);
    }
}
