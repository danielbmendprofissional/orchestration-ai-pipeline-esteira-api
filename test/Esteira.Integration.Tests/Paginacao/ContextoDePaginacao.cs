using System.Data.Common;
using Esteira.Application.Common.Paginacao;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Esteira.Integration.Tests.Paginacao
{
    // Tabela só de teste, num schema próprio criado pelo teste, fora das migrations da Esteira.
    public sealed class ItemDeTeste
    {
        public long Id { get; set; }
        public string Nome { get; set; } = "";
        public int Grupo { get; set; }
    }

    // DTO de lista com propriedades init: o EF só ordena depois do Select se a projeção for member-init, não por construtor.
    public sealed record ItemDeTesteDto
    {
        public long Id { get; init; }
        public string Nome { get; init; } = "";
        public int Grupo { get; init; }
    }

    public sealed class ContextoDePaginacao(DbContextOptions<ContextoDePaginacao> opcoes) : DbContext(opcoes)
    {
        public const string Schema = "paginacao_teste";

        public DbSet<ItemDeTeste> Itens => Set<ItemDeTeste>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);
            modelBuilder.Entity<ItemDeTeste>(e =>
            {
                e.ToTable("itens");
                e.HasKey(i => i.Id);
                e.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(i => i.Nome).HasColumnName("nome");
                e.Property(i => i.Grupo).HasColumnName("grupo");
            });
        }

        public static DbContextOptions<ContextoDePaginacao> Opcoes(string connectionString, params IInterceptor[] interceptores)
            => new DbContextOptionsBuilder<ContextoDePaginacao>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptores)
                .Options;

        // Idempotente (lock consultivo): classes de teste em paralelo semeiam o mesmo schema. Cria e semeia 25 itens que repetem valor em Grupo (3 grupos).
        public static async Task SemearAsync(InfraestruturaFixture infra)
        {
            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"""
                select pg_advisory_lock(7001);
                create schema if not exists {Schema};
                create table if not exists {Schema}.itens (id bigint primary key, nome text not null, grupo int not null);
                insert into {Schema}.itens select i, 'item-' || lpad(i::text, 2, '0'), i % 3 from generate_series(1, 25) i on conflict do nothing;
                select pg_advisory_unlock(7001);
                """;
            await comando.ExecuteNonQueryAsync();
        }

        public static OrdenacaoPadrao<ItemDeTesteDto> OrdenacaoPadrao => OrdenacaoPadrao<ItemDeTesteDto>.Por(i => i.Id);
    }

    // Guarda o SQL de cada consulta para provar o que foi pro banco (critério 9).
    public sealed class SqlCapturado : DbCommandInterceptor
    {
        private readonly List<string> _comandos = [];

        public IReadOnlyList<string> Comandos
        {
            get { lock (_comandos) return _comandos.ToList(); }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            lock (_comandos) _comandos.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
