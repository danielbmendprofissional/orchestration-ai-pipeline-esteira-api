using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence
{
    // ADR-0016: tudo da Esteira no schema "esteira" — tabelas (HasDefaultSchema nos contextos)
    // e o histórico de migrations. Única forma de ligar os contextos ao Postgres: API, dotnet ef e testes.
    public static class EsteiraPostgres
    {
        public const string Schema = "esteira";

        public static DbContextOptionsBuilder UsarPostgres(this DbContextOptionsBuilder options, string connectionString)
            => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));

        public static DbContextOptionsBuilder<TContexto> UsarPostgres<TContexto>(this DbContextOptionsBuilder<TContexto> options, string connectionString)
            where TContexto : DbContext
            => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }
}
