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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Esteira.Integration.Tests.Fatias
{
    // P2, critério 6 (corrida): Aprovar e Reprovar disparados juntos na mesma Fatia deixam uma só decisão pedida; a outra
    // recebe fatia.decisao-em-andamento. Postgres real: a trava é pg_advisory_xact_lock por Aplicação+Fatia.
    [Trait("Fatia", "P2")]
    public class DecisaoConcorrenteIntegracaoTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed record Cenario(long AppId, Guid AppPublicId, string Sub, AplicacaoNoPainel App);

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
            return new Cenario(app.Id, app.PublicId, sub, noPainel);
        }

        private static PreparadorDeDecisao Preparador(Cenario c)
        {
            var usuario = Substitute.For<IUsuarioAtual>();
            usuario.Id.Returns(c.Sub);
            var acesso = Substitute.For<IAcessoAplicacaoQuery>();
            acesso.ResolverIdAsync(c.AppPublicId, Arg.Any<CancellationToken>()).Returns(c.AppId);
            var autorizacao = Substitute.For<IAutorizacaoAplicacao>();
            autorizacao.AutorizarAsync(c.AppId, OperacaoAplicacao.DecidirFatia, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            var apps = Substitute.For<IPainelAplicacaoQuery>();
            apps.ListarVisiveisAsync(c.Sub, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([c.App]);
            var painel = Substitute.For<IPainelDeFatias>();
            var fatia = new FatiaLida("P2", "Review", "—", true, [RepositorioFatia.Api], "alto", true, "specs/x.md", 7,
                new ResultadoDeEstado(EstadoDaFatia.EmRevisao, null, false, false), false,
                new DetalheDaFatia(null, null, [], [], [new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — Review", "opened", "feature/p2", "success")]));
            painel.ObterFrescoAsync(Arg.Any<AplicacaoDoPainel>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoDoPainel("x", SituacaoDoPainel.Ok, [fatia], DateTimeOffset.UtcNow, false, null));
            return new PreparadorDeDecisao(acesso, autorizacao, apps, painel, usuario);
        }

        private static async Task<Result<AcoesPedidasDto>> DecidirAsync(IHost host, Cenario c, bool aprovar, Barrier largada)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var sp = escopo.ServiceProvider;
            var usuario = Substitute.For<IUsuarioAtual>();
            usuario.Id.Returns(c.Sub);
            var auditoria = new AuditoriaEf(sp.GetRequiredService<EsteiraContext>(), usuario, TimeProvider.System);
            var pessoas = sp.GetRequiredService<IPessoaRepositorio>();
            var acoes = sp.GetRequiredService<IAcaoBotRepositorio>();
            var uow = sp.GetRequiredService<IUnitOfWork>();
            var publicador = sp.GetRequiredService<IPublishEndpoint>();
            largada.SignalAndWait(Ct);
            return aprovar
                ? await new AprovarFatiaHandler(Preparador(c), pessoas, acoes, uow, auditoria, publicador, TimeProvider.System)
                    .Handle(new AprovarFatiaCommand(c.AppPublicId, "P2"), Ct)
                : await new ReprovarFatiaHandler(Preparador(c), pessoas, acoes, uow, auditoria, publicador, TimeProvider.System)
                    .Handle(new ReprovarFatiaCommand(c.AppPublicId, "P2", "faltou teste"), Ct);
        }

        [Fact, DisplayName("6: Aprovar e Reprovar em paralelo na mesma Fatia deixam exatamente uma decisão pedida; a outra recebe fatia.decisao-em-andamento")]
        public async Task Aprovar_e_reprovar_paralelos_deixam_uma_decisao()
        {
            using var host = HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico());
            await host.StartAsync(Ct);

            for (var rodada = 0; rodada < 15; rodada++)
            {
                var c = await CriarCenarioAsync();
                var largada = new Barrier(2);

                var resultados = await Task.WhenAll(
                    Task.Run(() => DecidirAsync(host, c, aprovar: true, largada), Ct),
                    Task.Run(() => DecidirAsync(host, c, aprovar: false, largada), Ct));

                resultados.Count(r => r.IsSuccess).Should().Be(1, $"rodada {rodada}");
                resultados.Single(r => !r.IsSuccess).Errors.Should().ContainSingle().Which.Code.Should().Be("fatia.decisao-em-andamento");
                await using var ctx = ContextoDoCadastro.Novo(infra);
                var pedidas = await ctx.Set<AcaoBotEntity>()
                    .Where(a => a.AplicacaoId == c.AppId && a.Estado == EstadoAcaoBot.Pedida).ToListAsync(Ct);
                pedidas.Should().ContainSingle($"rodada {rodada}");
            }

            await host.StopAsync(Ct);
        }
    }
}
