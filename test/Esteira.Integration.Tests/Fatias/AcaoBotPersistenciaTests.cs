using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Fatias;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Fatias
{
    // P2 (spec 02-review-e-aprovacao.md, "Dados"; critérios 7 e 11): migration expand-only e unicidade da chave de idempotência.
    [Trait("Fatia", "P2")]
    public class AcaoBotPersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        private async Task<(long AppId, long ProjetoId, long PessoaId)> CenarioAsync()
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/p", "p", Stack.Dotnet);
            var pessoa = PessoaEntity.Criar(Guid.NewGuid().ToString(), "Fulano", "f@x.com", T0);
            ctx.AddRange(projeto, pessoa);
            await ctx.SaveChangesAsync(Ct);
            return (app.Id, projeto.Id, pessoa.Id);
        }

        private static AcaoBotEntity Acao((long AppId, long ProjetoId, long PessoaId) c, TipoAcaoBot tipo = TipoAcaoBot.Aprovar, int tentativa = 1, long? depende = null)
            => AcaoBotEntity.Criar(c.AppId, "p2", c.ProjetoId, 42, tipo, tentativa, depende, c.PessoaId, null, T0).Value;

        [Fact, DisplayName("Deve gravar e ler a AcaoBot pelo repositório, com estado e tipo em texto kebab-case")]
        public async Task GravaELe()
        {
            var c = await CenarioAsync();
            var acao = AcaoBotEntity.Criar(c.AppId, "p2", c.ProjetoId, 42, TipoAcaoBot.Reprovar, 1, null, c.PessoaId, "ajustar X", T0).Value;

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                await new AcaoBotRepositorio(ctx).AdicionarAsync(acao, Ct);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var lida = await new AcaoBotRepositorio(ctx2).ObterPorChaveAsync(acao.ChaveIdempotencia, Ct);
            lida.Should().NotBeNull();
            (lida!.PublicId, lida.Tipo, lida.Estado, lida.Comentario, lida.PedidaEm).Should()
                .Be((acao.PublicId, TipoAcaoBot.Reprovar, EstadoAcaoBot.Pedida, "ajustar X", T0));

            var bruto = await ctx2.Database.SqlQuery<string>(
                $"SELECT \"Tipo\" || '/' || \"Estado\" AS \"Value\" FROM esteira.\"AcaoBot\" WHERE \"Id\" = {lida.Id}").SingleAsync(Ct);
            bruto.Should().Be("reprovar/pedida");
        }

        [Fact, DisplayName("Critério 7: o banco recusa a segunda Ação com a mesma chave de idempotência")]
        public async Task ChaveUnica()
        {
            var c = await CenarioAsync();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(Acao(c));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            ctx2.Add(Acao(c));
            var act = () => ctx2.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Deve aceitar nova tentativa (outra chave) e persistir a mudança de estado e a dependência")]
        public async Task NovaTentativaEEstado()
        {
            var c = await CenarioAsync();
            var api = Acao(c);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(api);
                await ctx.SaveChangesAsync(Ct);
                var front = Acao(c, depende: api.Id, tentativa: 2);
                ctx.Add(front);
                api.Iniciar(T0.AddMinutes(1));
                api.Falhar("gitlab.erro", T0.AddMinutes(2));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var ctx2 = ContextoDoCadastro.Novo(infra);
            var lida = await ctx2.Set<AcaoBotEntity>().AsNoTracking().SingleAsync(a => a.Id == api.Id, Ct);
            (lida.Estado, lida.CodigoErro, lida.AtualizadaEm).Should().Be((EstadoAcaoBot.Falhou, "gitlab.erro", T0.AddMinutes(2)));
            (await ctx2.Set<AcaoBotEntity>().AsNoTracking().CountAsync(a => a.DependeDeAcaoId == api.Id, Ct)).Should().Be(1);
        }

        [Fact, DisplayName("Deve recusar no banco Ação de Projeto ou pessoa inexistente")]
        public async Task ChavesEstrangeiras()
        {
            var c = await CenarioAsync();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            ctx.Add(Acao(c with { ProjetoId = long.MaxValue }));

            var act = () => ctx.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        }
    }
}
