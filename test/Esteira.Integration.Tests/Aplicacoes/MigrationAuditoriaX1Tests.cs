using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Events;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // X1.1 (critérios 11 e 13): backfill sobre dados existentes e trava de imutabilidade, contra PostgreSQL real.
    [Trait("Fatia", "X1")]
    public class MigrationAuditoriaX1Tests(InfraestruturaFixture infra)
    {
        private const string MigracaoAnterior = "20261128023031_Teto_X3";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<NpgsqlConnection> BancoNovoAsync(string nome)
        {
            await using (var admin = new NpgsqlConnection(infra.ConnectionString))
            {
                await admin.OpenAsync(Ct);
                await using var cmd = new NpgsqlCommand($"create database \"{nome}\"", admin);
                await cmd.ExecuteNonQueryAsync(Ct);
            }
            var cs = new NpgsqlConnectionStringBuilder(infra.ConnectionString) { Database = nome, Pooling = false, PersistSecurityInfo = true }.ConnectionString;
            var conexao = new NpgsqlConnection(cs);
            await conexao.OpenAsync(Ct);
            return conexao;
        }

        private static async Task<object?> Escalar(NpgsqlConnection c, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            return await cmd.ExecuteScalarAsync(Ct);
        }

        private static async Task Exec(NpgsqlConnection c, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }

        private static IMigrator Migrador(NpgsqlConnection c, out EsteiraContext ctx)
        {
            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(c.ConnectionString).Options;
            ctx = new EsteiraContext(opcoes, new SemDespacho());
            return ctx.GetService<IMigrator>();
        }

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        // uuid determinístico por número
        private static string Uuid(int n) => $"00000000-0000-0000-0000-{n:D12}";

        private static async Task SemearAsync(NpgsqlConnection c)
        {
            await Exec(c, $@"
insert into esteira.""Aplicacao"" (""Nome"",""Sigla"",""GrupoGitLabId"",""GrupoGitLabCaminho"",""Ativa"",""PublicId"") values ('App','APP',1,'g',true,'{Uuid(1)}');
insert into esteira.""Pessoa"" (""Sub"",""Nome"",""Email"",""UltimoLoginEm"",""PublicId"") values ('sub-ana','Ana Souza','a@x.com',now(),'{Uuid(2)}');
insert into esteira.""Projeto"" (""AplicacaoId"",""ProjetoGitLabId"",""Caminho"",""Nome"",""Stack"",""Ativo"",""PublicId"") values (1,10,'g/p','Proj','dotnet',true,'{Uuid(3)}');
insert into esteira.""AmbienteProjeto"" (""ProjetoId"",""Ambiente"",""Habilitado"",""PublicId"") values (1,'qa',true,'{Uuid(4)}');
insert into esteira.""Destino"" (""ProjetoId"",""Ambiente"",""Nome"",""Configuracao"",""Ordem"",""PublicId"") values (1,'qa','d','{{}}',0,'{Uuid(5)}');
insert into esteira.""PoliticaSeveridade"" (""AplicacaoId"",""Ambiente"",""Modo"",""PublicId"") values (1,'qa','avisa','{Uuid(6)}');
insert into esteira.""PapelAplicacao"" (""AplicacaoId"",""Usuario"",""Papel"",""PublicId"") values (1,'sub-ana','membro','{Uuid(7)}');
insert into esteira.""AcaoBot"" (""AplicacaoId"",""FatiaCodigo"",""ProjetoId"",""MrIid"",""Tipo"",""Estado"",""ChaveIdempotencia"",""PedidaPorPessoaId"",""PedidaEm"",""AtualizadaEm"",""PublicId"")
  values (1,'V4',1,5,'aprovar','concluida','k1',1,now(),now(),'{Uuid(8)}');");

            // Um registro por entidade conhecida, um do webhook e um de entidade desconhecida (usuário sem Pessoa).
            var linhas = new (string Entidade, int Pub, string Usuario, string Quando)[]
            {
                ("Aplicacao", 1, "sub-ana", "2026-01-01T10:00:00Z"),
                ("Projeto", 3, "sub-ana", "2026-01-02T10:00:00Z"),
                ("AmbienteProjeto", 4, "sub-ana", "2026-01-03T10:00:00Z"),
                ("Destino", 5, "sub-ana", "2026-01-04T10:00:00Z"),
                ("PoliticaSeveridade", 6, "sub-ana", "2026-01-05T10:00:00Z"),
                ("PapelAplicacao", 7, "sub-ana", "2026-01-06T10:00:00Z"),
                ("AcaoBot", 8, "sub-ana", "2026-01-07T10:00:00Z"),
                ("AcaoBot", 8, "webhook-gitlab", "2026-01-08T10:00:00Z"),
                ("Coisa", 9, "sub-fantasma", "2026-01-09T10:00:00Z"),
            };
            foreach (var l in linhas)
                await Exec(c, $@"insert into esteira.""RegistroAuditoria"" (""Quando"",""Usuario"",""Entidade"",""EntidadePublicId"",""Acao"",""Antes"",""Depois"",""PublicId"")
                    values ('{l.Quando}','{l.Usuario}','{l.Entidade}','{Uuid(l.Pub)}','criou','{{""a"":1}}','{{""a"":2}}',gen_random_uuid());");
        }

        [Fact, DisplayName("Deve preencher AplicacaoId, Categoria, Origem, UsuarioNome e ObjetoCodigo por entidade, sem perder registro nem alterar Quando/Antes/Depois; desconhecida fica sem Aplicação em cadastro")]
        public async Task Backfill_PorEntidade()
        {
            await using var c = await BancoNovoAsync("x1_backfill");
            var migrador = Migrador(c, out var ctx);
            await using var _ = ctx;
            await migrador.MigrateAsync(MigracaoAnterior, Ct);
            await SemearAsync(c);

            await migrador.MigrateAsync(null, Ct);

            (await Escalar(c, "select count(*) from esteira.\"RegistroAuditoria\"")).Should().Be(9L);

            async Task<(object?, object?, object?, object?, object?)> L(string ent, string usuario)
            {
                await using var cmd = new NpgsqlCommand(
                    "select \"AplicacaoId\",\"Categoria\",\"Origem\",\"UsuarioNome\",\"ObjetoCodigo\" from esteira.\"RegistroAuditoria\" where \"Entidade\"=@e and \"Usuario\"=@u", c);
                cmd.Parameters.AddWithValue("e", ent);
                cmd.Parameters.AddWithValue("u", usuario);
                await using var r = await cmd.ExecuteReaderAsync(Ct);
                (await r.ReadAsync(Ct)).Should().BeTrue();
                object? V(int i) => r.IsDBNull(i) ? null : r.GetValue(i);
                return (V(0), V(1), V(2), V(3), V(4));
            }

            foreach (var (ent, cat) in new[] { ("Aplicacao", "cadastro"), ("Projeto", "cadastro"), ("AmbienteProjeto", "cadastro"),
                         ("Destino", "cadastro"), ("PoliticaSeveridade", "politica"), ("PapelAplicacao", "papeis") })
                (await L(ent, "sub-ana")).Should().Be(((object?)1L, (object?)cat, (object?)"tela", (object?)"Ana Souza", (object?)null), ent);

            (await L("AcaoBot", "sub-ana")).Should().Be(((object?)1L, (object?)"fatias", (object?)"tela", (object?)"Ana Souza", (object?)"V4"));
            (await L("AcaoBot", "webhook-gitlab")).Should().Be(((object?)1L, (object?)"fatias", (object?)"pipeline", (object?)"webhook-gitlab", (object?)"V4"));
            (await L("Coisa", "sub-fantasma")).Should().Be(((object?)null, (object?)"cadastro", (object?)"tela", (object?)"sub-fantasma", (object?)null));

            (await Escalar(c, "select count(*) from esteira.\"RegistroAuditoria\" where \"Autoaprovacao\"")).Should().Be(0L);
            (await Escalar(c, "select \"Quando\" = '2026-01-01T10:00:00Z' and \"Antes\"::text = '{\"a\": 1}' and \"Depois\"::text = '{\"a\": 2}' from esteira.\"RegistroAuditoria\" where \"Entidade\"='Aplicacao'"))
                .Should().Be(true);
        }

        [Fact, DisplayName("Deve criar os índices (AplicacaoId, Quando desc) e (ObjetoCodigo) mantendo os existentes")]
        public async Task Indices()
        {
            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("select indexdef from pg_indexes where schemaname='esteira' and tablename='RegistroAuditoria'", conexao);
            var defs = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(Ct);
            while (await r.ReadAsync(Ct)) defs.Add(r.GetString(0));

            defs.Should().Contain(d => d.Contains("(\"AplicacaoId\", \"Quando\" DESC)"));
            defs.Should().Contain(d => d.Contains("(\"ObjetoCodigo\")"));
            defs.Should().Contain(d => d.Contains("(\"Entidade\", \"EntidadePublicId\")"));
            defs.Should().Contain(d => d.Contains("(\"Quando\")"));
        }

        [Fact, DisplayName("Deve recusar UPDATE e DELETE em RegistroAuditoria por SQL cru, aceitar INSERT, e a trava sai no Down")]
        public async Task Trava_Imutabilidade()
        {
            await using var c = await BancoNovoAsync("x1_trava");
            var migrador = Migrador(c, out var ctx);
            await using var _ = ctx;
            await migrador.MigrateAsync(null, Ct);

            await Exec(c, @"insert into esteira.""RegistroAuditoria"" (""Quando"",""Usuario"",""Entidade"",""EntidadePublicId"",""Acao"",""Categoria"",""PublicId"")
                values (now(),'u','Aplicacao',gen_random_uuid(),'criou','cadastro',gen_random_uuid())");

            var upd = async () => await Exec(c, "update esteira.\"RegistroAuditoria\" set \"Usuario\"='x'");
            var del = async () => await Exec(c, "delete from esteira.\"RegistroAuditoria\"");
            await upd.Should().ThrowAsync<PostgresException>();
            await del.Should().ThrowAsync<PostgresException>();
            (await Escalar(c, "select count(*) from esteira.\"RegistroAuditoria\"")).Should().Be(1L);

            await migrador.MigrateAsync(MigracaoAnterior, Ct);

            await Exec(c, "delete from esteira.\"RegistroAuditoria\"");
            (await Escalar(c, "select count(*) from esteira.\"RegistroAuditoria\"")).Should().Be(0L);
        }
    }
}
