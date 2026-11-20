using System.Linq.Expressions;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence.Extensions
{
    public static class PaginacaoExtensions
    {
        // Contagem, ordenação, Skip e Take no banco (ADR-0002). A ordenação padrão é obrigatória:
        // vale sem `sort` e desempata com `sort`. `sort` é propriedade do DTO, não do modelo, então a
        // projeção deve ser member-init (`new Dto { A = m.A }`): o EF não ordena por propriedade de DTO construído por construtor.
        public static async Task<Result<PaginaDto<TDto>>> PaginarAsync<TModel, TDto>(
            this IQueryable<TModel> query,
            PaginacaoDto paginacao,
            Expression<Func<TModel, TDto>> projecao,
            OrdenacaoPadrao<TDto> ordenacaoPadrao,
            CancellationToken cancellationToken = default)
            where TModel : class
        {
            ArgumentNullException.ThrowIfNull(paginacao);
            ArgumentNullException.ThrowIfNull(ordenacaoPadrao);

            var temSort = !string.IsNullOrWhiteSpace(paginacao.Ordenacao);
            if (temSort && !OrdenacaoExtensions.TemCampo<TDto>(paginacao.Ordenacao))
                return PaginacaoErros.OrdenacaoInvalida(paginacao.Ordenacao!);

            var total = await query.CountAsync(cancellationToken);

            // long: página e tamanho vêm do cliente, e (Pagina - 1) * Tamanho estoura int. Passou do total, nem consulta.
            var pular = (long)(paginacao.Pagina - 1) * paginacao.TamanhoPagina;
            if (pular >= total)
                return Result<PaginaDto<TDto>>.Sucesso(new PaginaDto<TDto>
                {
                    Itens = [],
                    Pagina = paginacao.Pagina,
                    TamanhoPagina = paginacao.TamanhoPagina,
                    Total = total
                });

            var projetada = query.Select(projecao);
            var ordenada = ordenacaoPadrao.Aplicar(
                projetada,
                temSort ? projetada.OrdenarPor(paginacao.Ordenacao, paginacao.Sentido) : null);

            var itens = await ordenada
                .Skip((int)pular)
                .Take(paginacao.TamanhoPagina)
                .ToListAsync(cancellationToken);

            return Result<PaginaDto<TDto>>.Sucesso(new PaginaDto<TDto>
            {
                Itens = itens,
                Pagina = paginacao.Pagina,
                TamanhoPagina = paginacao.TamanhoPagina,
                Total = total
            });
        }
    }
}
