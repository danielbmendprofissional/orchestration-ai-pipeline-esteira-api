using Esteira.Application.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore.Storage;

namespace Esteira.Data.Common.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly EsteiraContext _context;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(EsteiraContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task CommitAsync()
        {
            await _context.SaveChangesAsync();

            if (_transaction is null)
                return;

            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        public async Task RollbackAsync()
        {
            if (_transaction is null)
                return;

            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
