using System.Linq.Expressions;
using System.Reflection;


namespace Esteira.Application.Common.Extensions
{
    public static class OrdenacaoExtensions
    {
        public static IQueryable<T> OrdenarPor<T>(this IQueryable<T> query, string campo, string sentido)
        {
            if (string.IsNullOrEmpty(campo))
                return query;

            var propriedade = typeof(T).GetProperty(campo, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new ArgumentException($"Campo de ordenação inválido: {campo}");

            var parametro = Expression.Parameter(typeof(T), "x");
            var seletor = Expression.Lambda(Expression.Property(parametro, propriedade), parametro);
            var descendente = string.Equals(sentido, "desc", StringComparison.OrdinalIgnoreCase);

            var chamada = Expression.Call(
                typeof(Queryable),
                descendente ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy),
                new[] { typeof(T), propriedade.PropertyType },
                query.Expression,
                Expression.Quote(seletor));

            return query.Provider.CreateQuery<T>(chamada);
        }
    }
}
