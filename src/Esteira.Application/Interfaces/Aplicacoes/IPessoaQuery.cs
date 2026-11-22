using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Busca em Pessoa (EsteiraReadContext) para o MOR escolher quem vincular.
    public interface IPessoaQuery
    {
        Task<Result<PaginaDto<PessoaListaDto>>> ListarAsync(PaginacaoDto paginacao, string? busca, CancellationToken cancellationToken = default);
    }
}
