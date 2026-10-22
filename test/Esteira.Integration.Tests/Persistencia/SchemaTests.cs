using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Integration.Tests.Infraestrutura;
using Npgsql;

namespace Esteira.Integration.Tests.Persistencia
{
    // ADR-0016: tudo da Esteira no schema "esteira", nada no public.
    public class SchemaTests(InfraestruturaFixture infra)
    {
        private async Task<List<string>> TabelasDoSchemaAsync(string schema)
        {
            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(TestContext.Current.CancellationToken);
            await using var comando = new NpgsqlCommand(
                "select table_name from information_schema.tables where table_schema = @schema order by 1", conexao);
            comando.Parameters.AddWithValue("schema", schema);

            var tabelas = new List<string>();
            await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
                tabelas.Add(leitor.GetString(0));
            return tabelas;
        }

        [Fact, DisplayName("Deve criar as tabelas e o histórico de migrations no schema esteira")]
        public async Task Migrations_DevemCriarNoSchemaEsteira()
        {
            (await TabelasDoSchemaAsync("esteira"))
                .Should().Contain(["OutboxMessage", "OutboxState", "InboxState", "__EFMigrationsHistory"]);
        }

        [Fact, DisplayName("Não deve criar nada no schema public")]
        public async Task Migrations_NaoDevemCriarNoPublic()
        {
            (await TabelasDoSchemaAsync("public")).Should().BeEmpty();
        }
    }
}
