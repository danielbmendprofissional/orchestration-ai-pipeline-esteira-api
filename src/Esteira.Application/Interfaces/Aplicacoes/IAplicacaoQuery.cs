using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Leitura da Aplicação (EsteiraReadContext).
    public interface IAplicacaoQuery
    {
        // MOR vê todas; os demais, só as Aplicações em que têm papel.
        Task<Result<PaginaDto<AplicacaoListaDto>>> ListarAsync(
            PaginacaoDto paginacao, string? busca, bool? ativa, string sub, bool ehMor, CancellationToken cancellationToken = default);

        Task<AplicacaoDetalheDto?> ObterDetalheAsync(long id, CancellationToken cancellationToken = default);
    }
}
