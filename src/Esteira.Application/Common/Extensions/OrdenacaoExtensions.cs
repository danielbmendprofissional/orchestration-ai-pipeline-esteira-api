using System.Linq.Expressions;
using System.Reflection;

namespace Esteira.Application.Common.Extensions
{
    // Ordena por nome de propriedade (como sai no JSON, sem diferenciar maiúsculas). Não lança:
    // quem chama confere TemCampo antes, e o Result da falha sai do PaginarAsync.
    public static class OrdenacaoExtensions
    {
        public static bool TemCampo<T>(string? campo) => Propriedade(typeof(T), campo) is not null;

        public static IOrderedQueryable<T>? OrdenarPor<T>(this IQueryable<T> query, string? campo, string? sentido)
            => Aplicar(query, campo, Descendente(sentido), desempate: null);

        public static IOrderedQueryable<T>? DesempatarPor<T>(this IOrderedQueryable<T> query, string? campo, string? sentido)
            => Aplicar(query, campo, Descendente(sentido), desempate: query);

        private static bool Descendente(string? sentido)
            => string.Equals(sentido, "desc", StringComparison.OrdinalIgnoreCase);

        private static PropertyInfo? Propriedade(Type tipo, string campo)
            => string.IsNullOrWhiteSpace(campo)
                ? null
                : tipo.GetProperty(campo, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        private static IOrderedQueryable<T>? Aplicar<T>(IQueryable<T> query, string? campo, bool descendente, IOrderedQueryable<T>? desempate)
        {
            var propriedade = Propriedade(typeof(T), campo);
            if (propriedade is null)
                return null;

            var parametro = Expression.Parameter(typeof(T), "x");
            var seletor = Expression.Lambda(Expression.Property(parametro, propriedade), parametro);

            var metodo = (desempate is not null, descendente) switch
            {
                (false, false) => nameof(Queryable.OrderBy),
                (false, true) => nameof(Queryable.OrderByDescending),
                (true, false) => nameof(Queryable.ThenBy),
                (true, true) => nameof(Queryable.ThenByDescending)
            };

            var chamada = Expression.Call(
                typeof(Queryable),
                metodo,
                [typeof(T), propriedade.PropertyType],
                query.Expression,
                Expression.Quote(seletor));

            return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(chamada);
        }
    }
}
