using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Entregas;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias;
using Esteira.Data.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Entregas;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Esteira.Integration.Tests.Pipeline
{
    // E1b.3, criterios 5, 8 e 10 (PostgreSQL real, barreira): o webhook (success da pipeline antiga) e o `esteira-abrir`
    // (nova pipeline) gravam a mesma Etapa ao mesmo tempo. Sem 500, na ordem, sem perder o pipeline_id do esteira-abrir.
    [Trait("Fatia", "E1b")]
    public sealed class WebhookEtapaConcorrenteTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const long PipelineAntiga = 111;
        private const long PipelineNova = 222;

        // A primeira gravação de cada Unit of Work espera a outra chamada ter lido a Etapa; as demais (reaplicação) passam direto.
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

        private IHost NovoHost(Barrier barreira)
        {
            return HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), servicos: s =>
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
        }

        private async Task<EtapaFluxoEntity> LerEtapaAsync(long fluxoId)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<EtapaFluxoEntity>().AsNoTracking().SingleAsync(e => e.FluxoId == fluxoId && e.Etapa == EtapaDoFluxo.Qa, Ct);
        }

        private async Task<CenarioDePipeline> CenarioComQaExecutandoAsync(DateTime em)
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var fluxo = (await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo.PublicId, Ct))!;
            fluxo.RegistrarSha(CenarioDePipeline.Sha).IsSuccess.Should().BeTrue();
            fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(PipelineAntiga, em).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync(Ct);
            return c;
        }

        private async Task<Esteira.Domain.Common.Resultados.Result> WebhookAsync(IHost host, CenarioDePipeline c, string status, string finishedAt, Barrier? largada)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var tratador = escopo.ServiceProvider.GetRequiredService<ITratadorDeWebhookGitLab>();
            var corpo = $$$"""{"object_attributes":{"id":{{{PipelineAntiga}}},"status":"{{{status}}}","ref":"main","sha":"{{{CenarioDePipeline.Sha}}}","finished_at":"{{{finishedAt}}}"},"project":{"path_with_namespace":"{{{c.Caminho}}}"}}""";
            return await tratador.TratarAsync("Pipeline Hook", corpo, Ct);
        }

        private async Task<Esteira.Domain.Common.Resultados.Result<AbrirPipelineResposta>> AbrirAsync(CenarioDePipeline c, Barrier barreira)
        {
            await using var escrita = ContextoDoCadastro.Novo(infra);
            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var handler = new AbrirPipelineHandler(
                new ProjetoPorCaminhoQuery(leitura), new EntregaRepositorio(escrita), new ProjetoRepositorio(escrita),
                new OcupacaoAmbienteRepositorio(escrita), new DestinosDaPipelineQuery(leitura),
                new UnitOfWorkComBarreira(new UnitOfWork(escrita), barreira), TimeProvider.System, opcoes: new OpcoesLigadas());
            return await handler.ExecutarAsync(
                new AbrirPipelineEntrada("fluxo", "qa", c.Entrega.PublicId, c.Fluxo.PublicId, CenarioDePipeline.Sha, c.Caminho,
                    PipelineNova.ToString(), null, null, null, false), Ct);
        }

        [Fact, DisplayName("10: webhook (success da pipeline antiga) e esteira-abrir (pipeline nova) na mesma Etapa: sem erro, e a Etapa fica executando com o pipeline_id novo")]
        public async Task WebhookEAbrirSimultaneos_NaoPerdemOPipelineId()
        {
            for (var rodada = 0; rodada < 6; rodada++)
            {
                using var barreira = new Barrier(2);
                using var host = NovoHost(barreira);
                await host.StartAsync(Ct);
                var c = await CenarioComQaExecutandoAsync(DateTime.UtcNow.AddMinutes(-10));
                var fim = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

                var webhook = Task.Run(() => WebhookAsync(host, c, "success", fim, null), Ct);
                var abrir = Task.Run(() => AbrirAsync(c, barreira), Ct);
                await Task.WhenAll(webhook, abrir);

                (await webhook).IsSuccess.Should().BeTrue($"rodada {rodada}");
                (await abrir).IsSuccess.Should().BeTrue($"rodada {rodada}");
                var etapa = await LerEtapaAsync(c.Fluxo.Id);
                etapa.PipelineGitLabId.Should().Be(PipelineNova, $"rodada {rodada}: o pipeline_id do esteira-abrir nao se perde");
                etapa.Estado.Should().Be(EstadoEtapa.Executando, $"rodada {rodada}");
                await host.StopAsync(Ct);
            }
        }

        [Fact, DisplayName("8: evento mais velho que o ultimo aplicado nao regride a Etapa (banco real)")]
        public async Task EventoMaisVelho_NaoRegride()
        {
            using var barreira = new Barrier(1);
            using var host = NovoHost(barreira);
            await host.StartAsync(Ct);
            var c = await CenarioComQaExecutandoAsync(DateTime.UtcNow.AddMinutes(-10));

            (await WebhookAsync(host, c, "success", DateTime.UtcNow.AddMinutes(-2).ToString("yyyy-MM-dd HH:mm:ss") + " UTC", null)).IsSuccess.Should().BeTrue();
            (await WebhookAsync(host, c, "failed", DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd HH:mm:ss") + " UTC", null)).IsSuccess.Should().BeTrue();

            var etapa = await LerEtapaAsync(c.Fluxo.Id);
            etapa.Estado.Should().Be(EstadoEtapa.AguardandoAprovacao);
            etapa.CodigoFalha.Should().BeNull();
            await host.StopAsync(Ct);
        }
    }
}
