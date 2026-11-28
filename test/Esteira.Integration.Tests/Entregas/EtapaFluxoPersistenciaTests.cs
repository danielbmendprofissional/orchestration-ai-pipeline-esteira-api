using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Common.Events;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Entregas;
using Esteira.Domain.Common.Events;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace Esteira.Integration.Tests.Entregas
{
    // E1b (spec 02-tela-da-entrega.md, "Dados"): tabela EtapaFluxo, migration expand-only com preenchimento dos Fluxos existentes.
    [Trait("Fatia", "E1b")]
    public class EtapaFluxoPersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static Migration Migracao()
            => (Migration)Activator.CreateInstance(
                typeof(EsteiraContext).Assembly.GetTypes().Single(t => t.Name == "Etapa_E1b" && typeof(Migration).IsAssignableFrom(t)))!;

        [Fact, DisplayName("A migration só cria a tabela EtapaFluxo (com índices) e preenche as Etapas: nada existente é alterado")]
        public void ExpandOnly()
        {
            var operacoes = Migracao().UpOperations;

            operacoes.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(["EtapaFluxo"]);
            operacoes.Should().OnlyContain(o => o is CreateTableOperation || o is CreateIndexOperation || o is SqlOperation || o is EnsureSchemaOperation);
            operacoes.OfType<CreateTableOperation>().Single().Schema.Should().Be("esteira");
        }

        [Fact, DisplayName("Critério 1: Entrega gravada leva as 4 Etapas por Fluxo, com texto kebab-case no banco")]
        public async Task GravaAsEtapas()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra, 2);

            await using var ctx = ContextoDoCadastro.Novo(infra);
            var lida = (await new EntregaRepositorio(ctx).ObterPorPublicIdAsync(c.Entrega.PublicId, Ct))!;

            foreach (var fluxo in lida.Fluxos)
                fluxo.Etapas.Select(e => (e.Etapa, e.Estado)).Should().BeEquivalentTo(
                [
                    (EtapaDoFluxo.Abertura, EstadoEtapa.Concluido), (EtapaDoFluxo.Qa, EstadoEtapa.NaoIniciado),
                    (EtapaDoFluxo.Hml, EstadoEtapa.NaoIniciado), (EtapaDoFluxo.Prd, EstadoEtapa.NaoIniciado)
                ]);

            var bruto = await ctx.Database.SqlQuery<string>(
                $"SELECT \"Etapa\" || '/' || \"Estado\" AS \"Value\" FROM esteira.\"EtapaFluxo\" WHERE \"FluxoId\" = {lida.Fluxos[0].Id} AND \"Etapa\" = 'qa'").SingleAsync(Ct);
            bruto.Should().Be("qa/nao-iniciado");
        }

        [Fact, DisplayName("Índice único FluxoId+Etapa: o banco recusa a mesma Etapa duas vezes no Fluxo")]
        public async Task EtapaUnicaPorFluxo()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"EtapaFluxo\" (\"PublicId\", \"FluxoId\", \"Etapa\", \"Estado\", \"UltimoEventoEm\", \"AtualizadaEm\") VALUES ({Guid.CreateVersion7()}, {c.Fluxo().Id}, 'qa', 'nao-iniciado', now(), now())", Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Transição persiste: executando guarda o pipeline_id e o token xmin recusa gravação concorrente")]
        public async Task PersisteTransicaoEToken()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var t = CenarioDeEntrega.T0.AddMinutes(1);

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var fluxo = (await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!;
                fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(4321, t).IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync(Ct);
            }

            await using var a = ContextoDoCadastro.Novo(infra);
            await using var b = ContextoDoCadastro.Novo(infra);
            var ea = (await new EntregaRepositorio(a).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!.Etapa(EtapaDoFluxo.Qa);
            var eb = (await new EntregaRepositorio(b).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!.Etapa(EtapaDoFluxo.Qa);
            (ea.Estado, ea.PipelineGitLabId).Should().Be((EstadoEtapa.Executando, 4321L));

            ea.AplicarStatusDaPipeline("success", t.AddMinutes(1));
            eb.AplicarStatusDaPipeline("failed", t.AddMinutes(2));
            await a.SaveChangesAsync(Ct);
            var act = () => b.SaveChangesAsync(Ct);

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        [Fact, DisplayName("Critério 1 (migration): cada Fluxo já existente ganha as 4 Etapas, abertura concluido e o resto nao-iniciado")]
        public async Task PreencheFluxosExistentes()
        {
            var banco = $"bf_{Guid.NewGuid():N}";
            var admin = new NpgsqlConnectionStringBuilder(infra.ConnectionString);
            await using (var conexao = new NpgsqlConnection(admin.ConnectionString))
            {
                await conexao.OpenAsync(Ct);
                await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{banco}\"", conexao);
                await cmd.ExecuteNonQueryAsync(Ct);
            }

            admin.Database = banco;
            admin.Pooling = false;
            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(admin.ConnectionString).Options;
            await using var ctx = new EsteiraContext(opcoes, new SemDespacho());
            var migrator = ctx.GetService<IMigrator>();

            await migrator.MigrateAsync("Webhook_T1", Ct);
            // O preenchimento depende só de Fluxo existir: FKs ignoradas (replica) para não montar Aplicação/Projeto/Pessoa.
            await ctx.Database.ExecuteSqlRawAsync("""
                SET session_replication_role = replica;
                INSERT INTO esteira."Entrega" ("PublicId","AplicacaoId","Situacao","AbertaPorPessoaId","AbertaEm") VALUES ('00000000-0000-7000-8000-000000000001',1,'aberta',1,'2026-11-20T10:00:00Z');
                INSERT INTO esteira."Fluxo" ("PublicId","EntregaId","ProjetoId","TipoMudanca") VALUES ('00000000-0000-7000-8000-000000000002',1,1,'minor');
                INSERT INTO esteira."Fluxo" ("PublicId","EntregaId","ProjetoId","TipoMudanca") VALUES ('00000000-0000-7000-8000-000000000003',1,2,'minor');
                SET session_replication_role = DEFAULT;
                """, Ct);

            await migrator.MigrateAsync("Etapa_E1b", Ct);

            var linhas = await ctx.Database.SqlQuery<string>(
                $"SELECT e.\"FluxoId\" || ':' || e.\"Etapa\" || ':' || e.\"Estado\" || ':' || (e.\"UltimoEventoEm\" = '2026-11-20T10:00:00Z') AS \"Value\" FROM esteira.\"EtapaFluxo\" e").ToListAsync(Ct);
            linhas.Should().HaveCount(8).And.Contain(["1:abertura:concluido:true", "1:qa:nao-iniciado:true", "2:hml:nao-iniciado:true", "2:prd:nao-iniciado:true"]);
        }

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
