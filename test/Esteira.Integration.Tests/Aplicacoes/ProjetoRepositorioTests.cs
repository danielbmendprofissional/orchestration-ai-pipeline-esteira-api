using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // A1.4: consultas de escrita do Projeto (verificações prévias), tradução da corrida de unicidade e montagem do DTO.
    [Trait("Fatia", "A1")]
    public class ProjetoRepositorioTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<(EsteiraContext Ctx, AplicacaoEntity App, ProjetoEntity Projeto)> CenarioAsync()
        {
            var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("A", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/p", "p", Stack.Dotnet);
            ctx.Add(projeto);
            await ctx.SaveChangesAsync(Ct);
            foreach (var amb in Enum.GetValues<AmbienteDeploy>())
                ctx.Add(AmbienteProjetoEntity.Criar(projeto.Id, amb).Value);
            await ctx.SaveChangesAsync(Ct);
            return (ctx, app, projeto);
        }

        [Fact, DisplayName("Critérios 5 e 7: informa quais projetos do GitLab já estão cadastrados, em qualquer Aplicação")]
        public async Task ProjetosGitLabCadastrados()
        {
            var (ctx, _, projeto) = await CenarioAsync();
            await using var _ = ctx;
            var repo = new ProjetoRepositorio(ctx);
            var livre = ContextoDoCadastro.IdUnico();

            (await repo.ProjetoGitLabCadastradoAsync(projeto.ProjetoGitLabId, Ct)).Should().BeTrue();
            (await repo.ProjetoGitLabCadastradoAsync(livre, Ct)).Should().BeFalse();
            (await repo.ProjetosGitLabCadastradosAsync([projeto.ProjetoGitLabId, livre], Ct)).Should().BeEquivalentTo([projeto.ProjetoGitLabId]);
            (await repo.ProjetosGitLabCadastradosAsync([], Ct)).Should().BeEmpty();
        }

        [Fact, DisplayName("Critério 7: o índice único do ProjetoGitLabId sai como ViolacaoDeUnicidadeException")]
        public async Task CorridaDeProjeto()
        {
            var (ctx, app, projeto) = await CenarioAsync();
            await using var _ = ctx;
            await using var segundo = ContextoDoCadastro.Novo(infra);
            segundo.Add(ProjetoEntity.Criar(app.Id, projeto.ProjetoGitLabId, "g/outro", "outro", Stack.Outro));

            var act = () => new UnitOfWork(segundo).SaveChangesAsync();

            (await act.Should().ThrowAsync<ViolacaoDeUnicidadeException>()).Which.Restricao.Should().Contain("ProjetoGitLabId");
        }

        [Fact, DisplayName("Critério 10: nome de Destino é único por Projeto+Ambiente (verificação prévia, exceto o próprio, e índice)")]
        public async Task NomeDeDestino()
        {
            var (ctx, _, projeto) = await CenarioAsync();
            await using var _ = ctx;
            var repo = new ProjetoRepositorio(ctx);
            var destino = DestinoEntity.Criar(projeto.Id, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string>(), 0).Value;
            await repo.AdicionarDestinoAsync(destino, Ct);
            await ctx.SaveChangesAsync(Ct);

            (await repo.DestinoNomeExisteAsync(projeto.Id, AmbienteDeploy.Hml, "k8s", null, Ct)).Should().BeTrue();
            (await repo.DestinoNomeExisteAsync(projeto.Id, AmbienteDeploy.Hml, "k8s", destino.Id, Ct)).Should().BeFalse();
            (await repo.DestinoNomeExisteAsync(projeto.Id, AmbienteDeploy.Qa, "k8s", null, Ct)).Should().BeFalse();

            await using var segundo = ContextoDoCadastro.Novo(infra);
            segundo.Add(DestinoEntity.Criar(projeto.Id, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string>(), 1).Value);
            var act = () => new UnitOfWork(segundo).SaveChangesAsync();
            var violacao = (await act.Should().ThrowAsync<ViolacaoDeUnicidadeException>()).Which;
            violacao.Restricao.Should().Contain("Destino").And.Contain("Nome");
        }

        [Fact, DisplayName("Resolve Destino só dentro do Projeto e do Ambiente certos")]
        public async Task ObterDestino()
        {
            var (ctx, _, projeto) = await CenarioAsync();
            await using var _ = ctx;
            var repo = new ProjetoRepositorio(ctx);
            var destino = DestinoEntity.Criar(projeto.Id, AmbienteDeploy.Hml, "k8s", new Dictionary<string, string> { ["A"] = "1" }, 0).Value;
            await repo.AdicionarDestinoAsync(destino, Ct);
            await ctx.SaveChangesAsync(Ct);

            (await repo.ObterDestinoAsync(projeto.Id, AmbienteDeploy.Hml, destino.PublicId, Ct))!.Configuracao.Should().ContainKey("A");
            (await repo.ObterDestinoAsync(projeto.Id, AmbienteDeploy.Qa, destino.PublicId, Ct)).Should().BeNull();
            (await repo.ObterDestinoAsync(projeto.Id + 1000, AmbienteDeploy.Hml, destino.PublicId, Ct)).Should().BeNull();
        }

        [Fact, DisplayName("Dependências e Projetos da Aplicação saem só da Aplicação pedida")]
        public async Task DaAplicacao()
        {
            var (ctx, app, a) = await CenarioAsync();
            await using var _ = ctx;
            var b = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/b", "b", Stack.Outro);
            ctx.Add(b);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(DependenciaProjetoEntity.Criar(a.Id, b.Id).Value);
            await ctx.SaveChangesAsync(Ct);
            var (ctx2, app2, c) = await CenarioAsync();
            await using var _2 = ctx2;
            var repo = new ProjetoRepositorio(ctx);

            (await repo.ProjetosDaAplicacaoAsync(app.Id, Ct)).Select(p => p.Id).Should().BeEquivalentTo([a.Id, b.Id]);
            (await repo.DependenciasDaAplicacaoAsync(app.Id, Ct)).Select(d => (d.ProjetoId, d.DependeDeProjetoId)).Should().Equal((a.Id, b.Id));
            (await repo.DependenciasDaAplicacaoAsync(app2.Id, Ct)).Should().BeEmpty();
            (await repo.ProjetosDaAplicacaoAsync(app2.Id, Ct)).Select(p => p.Id).Should().Equal(c.Id);
        }

        [Fact, DisplayName("Monta o DTO do Projeto com Ambientes na ordem qa/hml/producao, Destinos por ordem e dependeDe em PublicId")]
        public async Task MontaDto()
        {
            var (ctx, app, a) = await CenarioAsync();
            await using var _ = ctx;
            var b = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/b", "b", Stack.Outro);
            ctx.Add(b);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(DependenciaProjetoEntity.Criar(a.Id, b.Id).Value);
            ctx.Add(DestinoEntity.Criar(a.Id, AmbienteDeploy.Hml, "segundo", new Dictionary<string, string>(), 2).Value);
            ctx.Add(DestinoEntity.Criar(a.Id, AmbienteDeploy.Hml, "primeiro", new Dictionary<string, string> { ["K"] = "v" }, 1).Value);
            await ctx.SaveChangesAsync(Ct);

            var dto = await new ProjetoRepositorio(ctx).MontarDtoAsync(a, Ct);

            dto.Id.Should().Be(a.PublicId);
            dto.Stack.Should().Be("dotnet");
            dto.DependeDe.Should().Equal(b.PublicId);
            dto.Ambientes.Select(x => x.Ambiente).Should().Equal("qa", "hml", "producao");
            var hml = dto.Ambientes.Single(x => x.Ambiente == "hml");
            hml.Destinos.Select(d => d.Nome).Should().Equal("primeiro", "segundo");
            hml.Destinos[0].Configuracao.Should().Contain("K", "v");
        }
    }
}
