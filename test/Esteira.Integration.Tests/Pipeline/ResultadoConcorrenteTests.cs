using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Analises.Gate;
using Esteira.Application.Analises.Resultado;
using Esteira.Application.Entregas;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Analises;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Entregas;
using Esteira.Domain.Analises;
using Esteira.Domain.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Esteira.Integration.Tests.Pipeline
{
    // E2.3, criterio 7 (PostgreSQL real, barreira): `esteira-resultado` e o webhook gravam a mesma Etapa ao mesmo tempo.
    // Sem 500, uma Analise por chave, e `bloqueada` prevalece sobre `aguardando-aprovacao` (a ordem das gravacoes nao importa).
    [Trait("Fatia", "E2")]
    public sealed class ResultadoConcorrenteTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long Pipeline = 111;

        private sealed class UnitOfWorkComBarreira(IUnitOfWork inner, Barrier barreira) : IUnitOfWork
        {
            private int _esperou;
            public Task BeginTransactionAsync() => inner.BeginTransactionAsync();
            public Task CommitAsync() => inner.CommitAsync();
            public Task RollbackAsync() => inner.RollbackAsync();
            public Task<int> SaveChangesAsync()
            {
                if (Interlocked.Exchange(ref _esperou, 1) == 0)
                    barreira.SignalAndWait(TimeSpan.FromSeconds(30));
                return inner.SaveChangesAsync();
            }
        }

        private sealed class OpcoesLigadas : IOpcoesDaEntrega
        {
            public bool E1bLigada => true;
        }

        private IHost NovoHost(Barrier barreira) => HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), servicos: s =>
        {
            s.AddDbContext<EsteiraReadContext>(o => o.UsarPostgres(infra.ConnectionString));
            var usuario = Substitute.For<Esteira.Application.Interfaces.Usuario.IUsuarioAtual>();
            usuario.Id.Returns("sistema");
            s.AddSingleton(usuario);
            s.AddSingleton(Substitute.For<Esteira.Application.Interfaces.Integracoes.IGitLab>());
            s.AddSingleton(Substitute.For<IDescarteDoPainel>());
            s.AddSingleton(Substitute.For<IPublicadorDeEventos>());
            s.AddScoped<IOpcoesDaEntrega, OpcoesLigadas>();
            s.AddScoped<IUnitOfWork>(sp => new UnitOfWorkComBarreira(new UnitOfWork(sp.GetRequiredService<EsteiraContext>()), barreira));
        });

        private async Task<CenarioDePipeline> CenarioAsync()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var fluxo = (await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo.PublicId, Ct))!;
            fluxo.RegistrarSha(CenarioDePipeline.Sha).IsSuccess.Should().BeTrue();
            fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(Pipeline, DateTime.UtcNow.AddMinutes(-10)).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync(Ct);
            return c;
        }

        private async Task<Esteira.Domain.Common.Resultados.Result> WebhookAsync(IHost host, CenarioDePipeline c)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var tratador = escopo.ServiceProvider.GetRequiredService<ITratadorDeWebhookGitLab>();
            var fim = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            var corpo = $$$"""{"object_attributes":{"id":{{{Pipeline}}},"status":"success","ref":"main","sha":"{{{CenarioDePipeline.Sha}}}","finished_at":"{{{fim}}}"},"project":{"path_with_namespace":"{{{c.Caminho}}}"}}""";
            return await tratador.TratarAsync("Pipeline Hook", corpo, Ct);
        }

        private async Task<Esteira.Domain.Common.Resultados.Result<GateResposta>> ResultadoAsync(CenarioDePipeline c, Barrier barreira)
        {
            await using var escrita = ContextoDoCadastro.Novo(infra);
            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var handler = new RegistrarResultadoHandler(
                new ProjetoPorCaminhoQuery(leitura), new EntregaRepositorio(escrita), new ProjetoRepositorio(escrita), new AplicacaoRepositorio(escrita),
                new AnaliseRepositorio(escrita), new SemExcecoesVigentesQuery(), new UnitOfWorkComBarreira(new UnitOfWork(escrita), barreira),
                TimeProvider.System, opcoes: new OpcoesLigadas());
            var achado = new GateAchadoCorpo("high", "XSS", "zap.xss", "a.cs", 1, "d");
            var corpo = new GateCorpo("fluxo", "qa", c.Entrega.PublicId.ToString(), c.Fluxo.PublicId.ToString(), CenarioDePipeline.Sha, c.Caminho,
                Pipeline.ToString(), [new GateAnaliseCorpo("zap", "ferramenta", "deploy", "reprovado", "k8s-a", null, [achado])]);
            return await handler.ExecutarAsync(c.Caminho, corpo, Ct);
        }

        [Fact, DisplayName("7: resultado bloqueante e webhook (success) simultaneos na mesma Etapa: sem erro, uma Analise por chave e a Etapa termina bloqueada")]
        public async Task ResultadoEWebhookSimultaneos_BloqueadaPrevalece()
        {
            for (var rodada = 0; rodada < 4; rodada++)
            {
                using var barreira = new Barrier(2);
                using var host = NovoHost(barreira);
                await host.StartAsync(Ct);
                var c = await CenarioAsync();

                var webhook = Task.Run(() => WebhookAsync(host, c), Ct);
                var resultado = Task.Run(() => ResultadoAsync(c, barreira), Ct);
                await Task.WhenAll(webhook, resultado);

                (await webhook).IsSuccess.Should().BeTrue($"rodada {rodada}");
                (await resultado).IsSuccess.Should().BeTrue($"rodada {rodada}");
                await using var ctx = ContextoDoCadastro.Novo(infra);
                (await ctx.Set<EtapaFluxoEntity>().AsNoTracking().SingleAsync(e => e.FluxoId == c.Fluxo.Id && e.Etapa == EtapaDoFluxo.Qa, Ct))
                    .Estado.Should().Be(EstadoEtapa.Bloqueada, $"rodada {rodada}");
                (await ctx.Set<AnaliseEntity>().CountAsync(a => a.FluxoId == c.Fluxo.Id, Ct)).Should().Be(1, $"rodada {rodada}");
                await host.StopAsync(Ct);
            }
        }

        [Fact, DisplayName("7: dois resultados identicos simultaneos (mesmo job): sem erro e uma Analise so")]
        public async Task DoisResultadosIdenticos_UmaAnalise()
        {
            using var barreira = new Barrier(2);
            var c = await CenarioAsync();

            var r = await Task.WhenAll(Task.Run(() => ResultadoAsync(c, barreira), Ct), Task.Run(() => ResultadoAsync(c, barreira), Ct));

            r.Should().OnlyContain(x => x.IsSuccess);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await ctx.Set<AnaliseEntity>().CountAsync(a => a.FluxoId == c.Fluxo.Id, Ct)).Should().Be(1);
        }
    }
}
