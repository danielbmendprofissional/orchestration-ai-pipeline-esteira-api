using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Specifications
{
    public static class SpecificationExtensions
    {
        public static IQueryable<T> Aplicar<T>(this IQueryable<T> query, Specification<T> specification) where T : class
        {
            foreach (var include in specification.Includes)
                query = include(query);

            if (specification.Criteria != null)
                query = query.Where(specification.Criteria);

            return query;
        }

        public static Task<bool> ExisteAsync<T>(this IQueryable<T> query, Specification<T> specification, CancellationToken cancellationToken = default) where T : class
            => specification.Criteria is null
                ? query.AnyAsync(cancellationToken)
                : query.AnyAsync(specification.Criteria, cancellationToken);
    }
}
