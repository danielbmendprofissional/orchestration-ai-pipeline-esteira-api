using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Common.Persistence;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace Esteira.Integration.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, "Dados"): migration expand-only, só tabelas e índices novos.
    [Trait("Fatia", "E1")]
    public class MigrationEntregaTests(InfraestruturaFixture infra)
    {
        private static readonly string[] Tabelas = ["Entrega", "Fluxo", "FluxoFatia", "OcupacaoAmbiente"];

        private static Migration Migracao()
            => (Migration)Activator.CreateInstance(
                typeof(EsteiraContext).Assembly.GetTypes().Single(t => t.Name == "Entrega_E1" && typeof(Migration).IsAssignableFrom(t)))!;

        [Fact, DisplayName("A migration só cria as quatro tabelas e seus índices: nada é alterado nem removido nas existentes")]
        public void ExpandOnly()
        {
            var operacoes = Migracao().UpOperations;

            operacoes.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(Tabelas);
            operacoes.Should().OnlyContain(o => o is CreateTableOperation || o is CreateIndexOperation || o is EnsureSchemaOperation);
            operacoes.OfType<CreateTableOperation>().Should().OnlyContain(o => o.Schema == "esteira");
            operacoes.OfType<CreateTableOperation>().SelectMany(o => o.ForeignKeys).Should().OnlyContain(fk => fk.PrincipalSchema == "esteira");
        }

        [Fact, DisplayName("As tabelas existem no schema esteira, com os índices únicos e sem nada no public")]
        public async Task TabelasEIndices()
        {
            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(TestContext.Current.CancellationToken);

            async Task<List<string>> Consulta(string sql)
            {
                await using var cmd = new NpgsqlCommand(sql, conexao);
                var l = new List<string>();
                await using var r = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken);
                while (await r.ReadAsync(TestContext.Current.CancellationToken)) l.Add(r.GetString(0));
                return l;
            }

            (await Consulta("select table_name from information_schema.tables where table_schema = 'esteira'")).Should().Contain(Tabelas);
            (await Consulta("select table_name from information_schema.tables where table_schema = 'public'")).Should().BeEmpty();

            var unicos = await Consulta("select indexdef from pg_indexes where schemaname = 'esteira' and indexdef like 'CREATE UNIQUE%' and tablename in ('Fluxo','FluxoFatia','OcupacaoAmbiente')");
            unicos.Should().Contain(d => d.Contains("(\"EntregaId\", \"ProjetoId\")"));
            unicos.Should().Contain(d => d.Contains("(\"FluxoId\", \"CodigoFatia\")"));
            unicos.Should().Contain(d => d.Contains("(\"ProjetoId\", \"Ambiente\")"));
        }
    }
}
