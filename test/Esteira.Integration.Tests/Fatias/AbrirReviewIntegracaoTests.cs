using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Esteira.Integration.Tests.Fatias
{
    // P2.2, critérios 1 e (corrida do duplo clique): Ação, auditoria e linha de outbox saem da mesma transação, no banco real.
    [Trait("Fatia", "P2")]
    public class AbrirReviewIntegracaoTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed record Cenario(long AppId, Guid AppPublicId, long ProjetoId, string Sub, AplicacaoNoPainel App);

        private async Task<Cenario> CriarCenarioAsync()
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), "g/api", "api", Stack.Dotnet);
            var sub = Guid.NewGuid().ToString();
            ctx.AddRange(projeto, PessoaEntity.Criar(sub, "Ana", "a@x.com", DateTime.UtcNow));
            await ctx.SaveChangesAsync(Ct);
            var noPainel = new AplicacaoNoPainel(app.PublicId, "App", app.Sigla, "g", 10, "g/ws",
                [new ProjetoNoPainel(RepositorioFatia.Api, projeto.ProjetoGitLabId, "g/api", projeto.Id)]);
            return new Cenario(app.Id, app.PublicId, projeto.Id, sub, noPainel);
        }

        private static AbrirReviewHandler Handler(IServiceProvider sp, Cenario c)
        {
            var usuario = Substitute.For<IUsuarioAtual>();
            usuario.Id.Returns(c.Sub);
            var acesso = Substitute.For<IAcessoAplicacaoQuery>();
            acesso.ResolverIdAsync(c.AppPublicId, Arg.Any<CancellationToken>()).Returns(c.AppId);
            var autorizacao = Substitute.For<IAutorizacaoAplicacao>();
            autorizacao.AutorizarAsync(c.AppId, OperacaoAplicacao.AbrirReview, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            var apps = Substitute.For<IPainelAplicacaoQuery>();
            apps.ListarVisiveisAsync(c.Sub, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([c.App]);
            var painel = Substitute.For<IPainelDeFatias>();
            var fatia = new FatiaLida("P2", "Review", "—", true, [RepositorioFatia.Api], "alto", true, "specs/x.md", 7,
                new ResultadoDeEstado(EstadoDaFatia.EmRevisao, null, false, false), false,
                new DetalheDaFatia(null, null, [], [], [new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — Review", "opened", "feature/p2", "success")]));
            painel.ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoDoPainel("x", SituacaoDoPainel.Ok, [fatia], DateTimeOffset.UtcNow, false, null));

            var contexto = sp.GetRequiredService<EsteiraContext>();
            return new AbrirReviewHandler(
                new PreparadorDeDecisao(acesso, autorizacao, apps, painel, usuario),
                sp.GetRequiredService<IPessoaRepositorio>(), sp.GetRequiredService<IAcaoBotRepositorio>(), sp.GetRequiredService<IUnitOfWork>(),
                new AuditoriaEf(contexto, usuario, TimeProvider.System), sp.GetRequiredService<IPublishEndpoint>(), TimeProvider.System);
        }

        private static async Task<Result<AcoesPedidasDto>> PedirAsync(IHost host, Cenario c)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            return await Handler(escopo.ServiceProvider, c).Handle(new AbrirReviewCommand(c.AppPublicId, "P2"), Ct);
        }

        private async Task<(int Acoes, int Outbox, int Auditoria)> ContarAsync(Cenario c)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var acao = await ctx.Set<AcaoBotEntity>().Where(a => a.AplicacaoId == c.AppId).ToListAsync(Ct);
            var chave = $"{c.AppId}:P2:abrir-review";
            var outbox = await ctx.Set<OutboxMessage>().CountAsync(m => m.Body.Contains(chave), Ct);
            var auditoria = await ctx.Set<RegistroAuditoriaEntity>().CountAsync(r => acao.Select(a => a.PublicId).Contains(r.EntidadePublicId), Ct);
            return (acao.Count, outbox, auditoria);
        }

        [Fact, DisplayName("1: grava a Ação, a auditoria e a linha de outbox AcaoBotPedida na mesma transação")]
        public async Task Grava_acao_auditoria_e_outbox()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico());
            await host.StartAsync(Ct);
            var c = await CriarCenarioAsync();

            var r = await PedirAsync(host, c);

            r.IsSuccess.Should().BeTrue();
            (await ContarAsync(c)).Should().Be((1, 1, 1));
            await host.StopAsync(Ct);
        }

        [Fact, DisplayName("Segundo pedido em sequência é recusado (fatia.review-ja-pedido) e não duplica Ação, auditoria nem outbox")]
        public async Task Sequencial_nao_duplica()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico());
            await host.StartAsync(Ct);
            var c = await CriarCenarioAsync();
            await PedirAsync(host, c);

            var segundo = await PedirAsync(host, c);

            segundo.Errors.Should().ContainSingle().Which.Code.Should().Be("fatia.review-ja-pedido");
            (await ContarAsync(c)).Should().Be((1, 1, 1));
            await host.StopAsync(Ct);
        }

        [Fact, DisplayName("Duplo clique concorrente cria uma Ação só: ninguém recebe erro inesperado e o banco fica com uma linha de cada")]
        public async Task Concorrente_cria_uma_so()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico());
            await host.StartAsync(Ct);
            var c = await CriarCenarioAsync();

            var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => PedirAsync(host, c), Ct)));

            resultados.Count(r => r.IsSuccess).Should().BeGreaterThanOrEqualTo(1);
            resultados.Where(r => !r.IsSuccess).Should().OnlyContain(r => r.Errors[0].Code == "fatia.review-ja-pedido");
            resultados.Where(r => r.IsSuccess).Select(r => r.Value.Acoes[0].Id).Distinct().Should().ContainSingle();
            (await ContarAsync(c)).Should().Be((1, 1, 1));
            await host.StopAsync(Ct);
        }
    }
}
