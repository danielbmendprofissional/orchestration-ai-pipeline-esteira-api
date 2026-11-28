using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // X3 (spec 02-teto-de-custo-dos-agentes.md, "Dados"): coluna nula numeric(6,2) com CHECK 0.10-50.00; migration expand-only.
    [Trait("Fatia", "X3")]
    public class TetoCustoPersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<AplicacaoEntity> NovaAsync()
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            return app;
        }

        [Fact, DisplayName("Deve nascer com teto nulo e gravar, ler e voltar ao padrão")]
        public async Task Teto_GravaLeELimpa()
        {
            var app = await NovaAsync();

            await using (var leitura = ContextoDoCadastro.NovoLeitura(infra))
            {
                var a = await leitura.Set<AplicacaoEntity>().AsNoTracking().SingleAsync(x => x.Id == app.Id, Ct);
                a.TetoCustoAgentesUsd.Should().BeNull();
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var a = await ctx.Set<AplicacaoEntity>().SingleAsync(x => x.Id == app.Id, Ct);
                a.DefinirTetoCusto(2.50m);
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var leitura = ContextoDoCadastro.NovoLeitura(infra))
            {
                var a = await leitura.Set<AplicacaoEntity>().AsNoTracking().SingleAsync(x => x.Id == app.Id, Ct);
                a.TetoCustoAgentesUsd.Should().Be(2.50m);
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var a = await ctx.Set<AplicacaoEntity>().SingleAsync(x => x.Id == app.Id, Ct);
                a.DefinirTetoCusto(null);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var limpa = await ctx2.Set<AplicacaoEntity>().AsNoTracking().SingleAsync(x => x.Id == app.Id, Ct);
            limpa.TetoCustoAgentesUsd.Should().BeNull();
        }

        [Theory, DisplayName("Deve recusar no banco teto fora da faixa 0.10-50.00")]
        [InlineData("0.09")]
        [InlineData("0")]
        [InlineData("50.01")]
        public async Task Teto_BancoRecusaForaDaFaixa(string valor)
        {
            var app = await NovaAsync();
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlRawAsync(
                $"UPDATE esteira.\"Aplicacao\" SET \"TetoCustoAgentesUsd\" = {valor} WHERE \"Id\" = {{0}}", [app.Id], Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        [Theory, DisplayName("Deve aceitar no banco os limites 0.10 e 50.00")]
        [InlineData("0.10")]
        [InlineData("50.00")]
        public async Task Teto_BancoAceitaLimites(string valor)
        {
            var app = await NovaAsync();
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var linhas = await ctx.Database.ExecuteSqlRawAsync(
                $"UPDATE esteira.\"Aplicacao\" SET \"TetoCustoAgentesUsd\" = {valor} WHERE \"Id\" = {{0}}", [app.Id], Ct);

            linhas.Should().Be(1);
        }

        [Fact, DisplayName("Deve ter a coluna numeric(6,2) nula e nenhum teto nas Aplicações existentes")]
        public async Task Teto_ColunaNumeric62Nula()
        {
            var app = await NovaAsync();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync(Ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data_type, numeric_precision, numeric_scale, is_nullable FROM information_schema.columns " +
                              "WHERE table_schema = 'esteira' AND table_name = 'Aplicacao' AND column_name = 'TetoCustoAgentesUsd'";
            await using var rd = await cmd.ExecuteReaderAsync(Ct);
            (await rd.ReadAsync(Ct)).Should().BeTrue();
            (rd.GetString(0), rd.GetInt32(1), rd.GetInt32(2), rd.GetString(3)).Should().Be(("numeric", 6, 2, "YES"));
            await rd.CloseAsync();

            cmd.CommandText = $"SELECT count(*) FROM esteira.\"Aplicacao\" WHERE \"TetoCustoAgentesUsd\" IS NOT NULL AND \"Id\" = {app.Id}";
            ((long)(await cmd.ExecuteScalarAsync(Ct))!).Should().Be(0);
        }
    }
}
