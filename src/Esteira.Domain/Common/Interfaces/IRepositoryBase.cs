using Esteira.Domain.Common.Base;
namespace Esteira.Domain.Common.Interfaces
{
    public interface IRepositoryBase<T> where T : class, IAggregateRoot
    {
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        void Remove(T entity);
    }

}
