using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // A1.3, corrida de unicidade: o 23505 do Postgres sai do UnitOfWork como ViolacaoDeUnicidadeException (com o nome do índice),
    // e o AplicacaoRepositorio responde às verificações prévias.
    [Trait("Fatia", "A1")]
    public class UnicidadeNoUnitOfWorkTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Theory, DisplayName("Deve traduzir a violação de índice único em ViolacaoDeUnicidadeException com o índice")]
        [InlineData(true, "Sigla")]
        [InlineData(false, "GrupoGitLabId")]
        public async Task SaveChanges_TraduzUnicidade(bool mesmaSigla, string indice)
        {
            var sigla = ContextoDoCadastro.SiglaUnica().ToUpperInvariant();
            var grupo = ContextoDoCadastro.IdUnico();
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(AplicacaoEntity.Criar("A", sigla, grupo, "g").Value);
                await ctx.SaveChangesAsync(Ct);
            }

            await using var segundo = ContextoDoCadastro.Novo(infra);
            segundo.Add(AplicacaoEntity.Criar("B", mesmaSigla ? sigla : ContextoDoCadastro.SiglaUnica(), mesmaSigla ? ContextoDoCadastro.IdUnico() : grupo, "g2").Value);

            var act = () => new UnitOfWork(segundo).SaveChangesAsync();

            (await act.Should().ThrowAsync<ViolacaoDeUnicidadeException>()).Which.Restricao.Should().Contain(indice);
        }

        [Fact, DisplayName("Deve responder às verificações prévias de sigla (exceto a própria) e de grupo")]
        public async Task Repositorio_Verificacoes()
        {
            var sigla = ContextoDoCadastro.SiglaUnica().ToUpperInvariant();
            var grupo = ContextoDoCadastro.IdUnico();
            var app = AplicacaoEntity.Criar("A", sigla, grupo, "g").Value;
            await using var ctx = ContextoDoCadastro.Novo(infra);
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var repo = new AplicacaoRepositorio(ctx);

            (await repo.SiglaExisteAsync(sigla, null, Ct)).Should().BeTrue();
            (await repo.SiglaExisteAsync(sigla, app.Id, Ct)).Should().BeFalse();
            (await repo.SiglaExisteAsync(ContextoDoCadastro.SiglaUnica(), null, Ct)).Should().BeFalse();
            (await repo.GrupoVinculadoAsync(grupo, Ct)).Should().BeTrue();
            (await repo.GrupoVinculadoAsync(ContextoDoCadastro.IdUnico(), Ct)).Should().BeFalse();
        }
    }
}
