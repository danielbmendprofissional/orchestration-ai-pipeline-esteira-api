using Esteira.Application.Auditoria;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Auditoria
{
    // Leitura da trilha (X1.3) no EsteiraReadContext. Todas as consultas passam pelo IEscopoDeAuditoria.
    public interface IAuditoriaQuery
    {
        Task<Result<PaginaDto<RegistroAuditoriaListaDto>>> ListarAsync(PaginacaoDto paginacao, FiltroDeAuditoria filtro, CancellationToken cancellationToken = default);

        // Todas as categorias (zero incluído); `filtro.Categoria` é ignorado de propósito.
        Task<IReadOnlyList<CategoriaContagemDto>> ContarPorCategoriaAsync(FiltroDeAuditoria filtro, CancellationToken cancellationToken = default);

        Task<FiltrosDeAuditoriaDto> FiltrosAsync(CancellationToken cancellationToken = default);
    }
}
