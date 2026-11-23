using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // P1 (spec 01-painel-de-fatias.md, "Dados"): migration expand-only e coerência do workspace no banco.
    [Trait("Fatia", "P1")]
    public class WorkspacePersistenciaTests(InfraestruturaFixture infra)
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

        [Fact, DisplayName("Deve gravar e ler o workspace da Aplicação, e limpar os dois campos")]
        public async Task Workspace_GravaLeELimpa()
        {
            var app = await NovaAsync();

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var a = await ctx.Set<AplicacaoEntity>().SingleAsync(x => x.Id == app.Id, Ct);
                a.DefinirWorkspace(77, "grupo/workspace");
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var leitura = ContextoDoCadastro.NovoLeitura(infra))
            {
                var a = await leitura.Set<AplicacaoEntity>().AsNoTracking().SingleAsync(x => x.Id == app.Id, Ct);
                (a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho).Should().Be((77L, "grupo/workspace"));
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var a = await ctx.Set<AplicacaoEntity>().SingleAsync(x => x.Id == app.Id, Ct);
                a.LimparWorkspace();
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var limpa = await ctx2.Set<AplicacaoEntity>().AsNoTracking().SingleAsync(x => x.Id == app.Id, Ct);
            (limpa.WorkspaceGitLabId, limpa.WorkspaceGitLabCaminho).Should().Be((null, null));
        }

        [Fact, DisplayName("Deve recusar no banco workspace com só um dos dois campos")]
        public async Task Workspace_BancoRecusaIncoerente()
        {
            var app = await NovaAsync();
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlRawAsync(
                "UPDATE esteira.\"Aplicacao\" SET \"WorkspaceGitLabId\" = 5 WHERE \"Id\" = {0}", [app.Id], Ct);

            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }
    }
}
