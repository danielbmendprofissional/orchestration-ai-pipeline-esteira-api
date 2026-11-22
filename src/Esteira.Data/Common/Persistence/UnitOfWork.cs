using Esteira.Application.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
            return await SalvarAsync();
        }

        // 23505 (índice único) vira exceção técnica da Application: o handler a traduz no erro da regra (corrida).
        private async Task<int> SalvarAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                throw new ViolacaoDeUnicidadeException(pg.ConstraintName, ex);
            }
        }

        public async Task CommitAsync()
        {
            await SalvarAsync();

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
