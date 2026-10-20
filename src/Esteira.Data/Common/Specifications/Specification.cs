using System.Linq.Expressions;

namespace Esteira.Data.Common.Specifications
{
    public abstract class Specification<T> where T : class
    {
        public Expression<Func<T, bool>> Criteria { get; protected set; }
        public List<Func<IQueryable<T>, IQueryable<T>>> Includes { get; } = new();
    }
}
