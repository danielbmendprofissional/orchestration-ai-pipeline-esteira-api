using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence
{
    public sealed class EsteiraReadContext : DbContext
    {
        public EsteiraReadContext(DbContextOptions<EsteiraReadContext> options)
            : base(options)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(EsteiraPostgres.Schema);

            // Cadastro da A1: as configurations de Write/ valem nos dois contextos (leitura usa as mesmas tabelas).
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(EsteiraContext).Assembly,
                t => t.Namespace is not null && t.Namespace.EndsWith(".Write", StringComparison.Ordinal));

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => throw new InvalidOperationException("O contexto de leitura não permite gravação.");

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("O contexto de leitura não permite gravação.");
    }
}
