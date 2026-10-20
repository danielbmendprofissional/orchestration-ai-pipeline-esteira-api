using System.Linq.Expressions;
using Esteira.Application.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Data.Common.Persistence.Extensions
{
    public static class PaginacaoExtensions
    {
        public static async Task<IReadOnlyList<TDto>> PaginarAsync<TModel, TDto>(
            this IQueryable<TModel> query,
            PaginacaoDto pagination,
            Expression<Func<TModel, TDto>> projecao,
            CancellationToken cancellationToken = default)
            where TModel : class
        {
            if (pagination is null)
                return await query.Select(projecao).ToListAsync(cancellationToken);

            pagination.TotalItems = await query.CountAsync(cancellationToken);

            return await query
                .Select(projecao)
                .OrdenarPor(pagination.Order, pagination.SortOrder)
                .Skip(pagination.Pular)
                .Take(pagination.Tamanho)
                .ToListAsync(cancellationToken);
        }
    }
}
