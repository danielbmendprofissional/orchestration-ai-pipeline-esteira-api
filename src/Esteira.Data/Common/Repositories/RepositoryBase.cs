using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Interfaces;

namespace Esteira.Data.Common.Repositories
{
    public abstract class RepositoryBase<T> : IRepositoryBase<T>
        where T : class, IAggregateRoot
    {
        protected readonly EsteiraContext Context;

        protected RepositoryBase(EsteiraContext context)
        {
            Context = context;
        }

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await Context.Set<T>().AddAsync(entity, cancellationToken);

        public void Remove(T entity)
            => Context.Set<T>().Remove(entity);
    }
}
