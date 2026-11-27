using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Eventos;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Eventos;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Eventos
{
    // T1 (spec 01-tempo-real, "Dados"; critérios 7 e 10): tabela nova no schema esteira, índice único por Projeto.
    [Trait("Fatia", "T1")]
    public class WebhookProjetoPersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);

        private async Task<long> NovoProjetoAsync(bool ativo = true)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/p", "p", Stack.Dotnet);
            if (!ativo) projeto.Alterar(Stack.Dotnet, false);
            ctx.Add(projeto);
            await ctx.SaveChangesAsync(Ct);
            return projeto.Id;
        }

        [Fact, DisplayName("Deve criar a tabela WebhookProjeto no schema esteira")]
        public async Task TabelaNoSchemaEsteira()
        {
            await using var conexao = new NpgsqlConnection(infra.ConnectionString);
            await conexao.OpenAsync(Ct);
            await using var comando = new NpgsqlCommand(
                "select count(*) from information_schema.tables where table_schema = 'esteira' and table_name = 'WebhookProjeto'", conexao);

            ((long)(await comando.ExecuteScalarAsync(Ct))!).Should().Be(1);
        }

        [Fact, DisplayName("Critério 7: deve gravar e ler a linha pendente pelo repositório, com Estado em texto kebab-case")]
        public async Task GravaELePendente()
        {
            var projetoId = await NovoProjetoAsync();
            var w = WebhookProjetoEntity.Criar(projetoId, T0);

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                await new WebhookProjetoRepositorio(ctx).AdicionarAsync(w, Ct);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var lida = await new WebhookProjetoRepositorio(ctx2).ObterPorProjetoAsync(projetoId, Ct);
            lida.Should().NotBeNull();
            (lida!.PublicId, lida.Estado, lida.GitLabHookId, lida.CodigoErro, lida.AtualizadaEm)
                .Should().Be((w.PublicId, EstadoWebhookProjeto.Pendente, (long?)null, (string?)null, T0));

            var bruto = await ctx2.Database.SqlQuery<string>(
                $"SELECT \"Estado\" AS \"Value\" FROM esteira.\"WebhookProjeto\" WHERE \"Id\" = {lida.Id}").SingleAsync(Ct);
            bruto.Should().Be("pendente");
        }

        [Fact, DisplayName("Deve persistir a transição para registrado")]
        public async Task PersisteRegistrado()
        {
            var projetoId = await NovoProjetoAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(WebhookProjetoEntity.Criar(projetoId, T0));
                await ctx.SaveChangesAsync(Ct);
            }

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var w = (await new WebhookProjetoRepositorio(ctx).ObterPorProjetoAsync(projetoId, Ct))!;
                w.Registrar(77, T0.AddMinutes(1));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var lida = await ctx2.Set<WebhookProjetoEntity>().AsNoTracking().SingleAsync(x => x.ProjetoId == projetoId, Ct);
            (lida.Estado, lida.GitLabHookId).Should().Be((EstadoWebhookProjeto.Registrado, (long?)77));
        }

        [Fact, DisplayName("Deve recusar no banco a segunda linha com o mesmo ProjetoId (índice único)")]
        public async Task IndiceUnicoPorProjeto()
        {
            var projetoId = await NovoProjetoAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(WebhookProjetoEntity.Criar(projetoId, T0));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            ctx2.Add(WebhookProjetoEntity.Criar(projetoId, T0));
            var act = () => ctx2.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Deve recusar no banco WebhookProjeto de Projeto inexistente")]
        public async Task ChaveEstrangeira()
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            ctx.Add(WebhookProjetoEntity.Criar(long.MaxValue, T0));
            var act = () => ctx.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        }

        [Fact, DisplayName("Critério 10 (base): deve listar os Projetos ativos sem WebhookProjeto")]
        public async Task ListaProjetosAtivosSemWebhook()
        {
            var com = await NovoProjetoAsync();
            var sem = await NovoProjetoAsync();
            var inativo = await NovoProjetoAsync(ativo: false);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(WebhookProjetoEntity.Criar(com, T0));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var ids = await new WebhookProjetoRepositorio(ctx2).ListarProjetosAtivosSemWebhookAsync(Ct);

            ids.Should().Contain(sem);
            ids.Should().NotContain([com, inativo]);
        }
    }
}
