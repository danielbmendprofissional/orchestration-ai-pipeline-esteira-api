using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Entregas;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, "Dados"): mapeamento, índices únicos e concorrência otimista, no PostgreSQL real.
    [Trait("Fatia", "E1")]
    public class EntregaPersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const string ShaA = "0123456789abcdef0123456789abcdef01234567";
        private const string ShaB = "fedcba9876543210fedcba9876543210fedcba98";

        [Fact, DisplayName("Deve gravar e ler a Entrega com Fluxos e Fatias, com texto kebab-case no banco")]
        public async Task GravaELe()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra, 2);

            await using var ctx = ContextoDoCadastro.Novo(infra);
            var lida = await new EntregaRepositorio(ctx).ObterPorPublicIdAsync(c.Entrega.PublicId, Ct);

            lida.Should().NotBeNull();
            (lida!.AplicacaoId, lida.Situacao, lida.AbertaPorPessoaId, lida.AbertaEm).Should()
                .Be((c.Aplicacao.Id, SituacaoEntrega.Aberta, c.Pessoa.Id, CenarioDeEntrega.T0));
            lida.Fluxos.Should().HaveCount(2);
            lida.Fluxos.All(f => f.EntregaId == lida.Id && f.Sha == null && f.Versao == null).Should().BeTrue();
            lida.Fluxos[0].Fatias.Select(f => (f.CodigoFatia, f.ComFlag)).Should().BeEquivalentTo([("B2", true), ("B3", false)]);

            var bruto = await ctx.Database.SqlQuery<string>(
                $"SELECT e.\"Situacao\" || '/' || f.\"TipoMudanca\" AS \"Value\" FROM esteira.\"Entrega\" e JOIN esteira.\"Fluxo\" f ON f.\"EntregaId\" = e.\"Id\" WHERE e.\"Id\" = {lida.Id} LIMIT 1").SingleAsync(Ct);
            bruto.Should().Be("aberta/minor");
        }

        [Fact, DisplayName("Deve achar o Fluxo pelo PublicId, com as Fatias")]
        public async Task FluxoPorPublicId()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);

            await using var ctx = ContextoDoCadastro.Novo(infra);
            var fluxo = await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct);

            fluxo.Should().NotBeNull();
            (fluxo!.ProjetoId, fluxo.Fatias.Count).Should().Be((c.Projetos[0].Id, 2));
        }

        [Fact, DisplayName("Índice único: o banco recusa dois Fluxos do mesmo Projeto na Entrega")]
        public async Task FluxoUnicoPorProjeto()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"Fluxo\" (\"PublicId\", \"EntregaId\", \"ProjetoId\", \"TipoMudanca\") VALUES ({Guid.CreateVersion7()}, {c.Entrega.Id}, {c.Projetos[0].Id}, 'major')", Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Índice único: o banco recusa a mesma Fatia duas vezes no Fluxo")]
        public async Task FatiaUnicaPorFluxo()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"FluxoFatia\" (\"PublicId\", \"FluxoId\", \"CodigoFatia\", \"ComFlag\") VALUES ({Guid.CreateVersion7()}, {c.Fluxo().Id}, 'B2', false)", Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Deve recusar no banco SHA fora de 40 hex minúsculo")]
        public async Task ShaInvalidoNoBanco()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync($"UPDATE esteira.\"Fluxo\" SET \"Sha\" = 'ABC' WHERE \"Id\" = {c.Fluxo().Id}", Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        [Fact, DisplayName("Deve persistir o SHA registrado e achá-lo de novo")]
        public async Task PersisteSha()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var fluxo = (await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!;
                fluxo.RegistrarSha(ShaA).IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            (await new EntregaRepositorio(ctx2).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!.Sha.Should().Be(ShaA);
        }

        [Fact, DisplayName("Concorrência otimista: duas gravações do mesmo Fluxo lido -> a segunda falha")]
        public async Task ConcorrenciaOtimista()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx1 = ContextoDoCadastro.Novo(infra);
            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var f1 = (await new EntregaRepositorio(ctx1).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!;
            var f2 = (await new EntregaRepositorio(ctx2).ObterFluxoPorPublicIdAsync(c.Fluxo().PublicId, Ct))!;
            f1.RegistrarSha(ShaA);
            f2.RegistrarSha(ShaB);

            await ctx1.SaveChangesAsync(Ct);
            var act = () => ctx2.SaveChangesAsync(Ct);

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        [Fact, DisplayName("Deve recusar no banco Entrega de Aplicação inexistente")]
        public async Task ChavesEstrangeiras()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"Entrega\" (\"PublicId\", \"AplicacaoId\", \"Situacao\", \"AbertaPorPessoaId\", \"AbertaEm\") VALUES ({Guid.CreateVersion7()}, {long.MaxValue}, 'aberta', {c.Pessoa.Id}, now())", Ct);

            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        }
    }
}
