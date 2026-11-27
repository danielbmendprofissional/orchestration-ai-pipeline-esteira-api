using System.Collections.Concurrent;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Eventos;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Esteira.Integration.Tests.Eventos
{
    // T1.3, critério 6: o webhook e a consulta periódica concluem a mesma Ação ao mesmo tempo (PostgreSQL real, barreira).
    // Uma conclusão vale; a outra é no-op: nunca exceção, nunca transição inválida, estado final `confirmada`.
    [Trait("Fatia", "T1")]
    public class WebhookConclusaoConcorrenteTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);

        private readonly ConcurrentDictionary<long, byte> _minhas = new();

        private sealed record Cenario(long AcaoId, Guid AcaoPublicId, string Caminho, long PipelineId);

        private async Task<Cenario> CriarAsync()
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            var caminho = $"g/{Guid.NewGuid():N}";
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), caminho, "front", Stack.Angular);
            var pessoa = PessoaEntity.Criar(Guid.NewGuid().ToString(), "Ana", "a@x.com", T0);
            ctx.AddRange(projeto, pessoa);
            await ctx.SaveChangesAsync(Ct);

            var pipelineId = ContextoDoCadastro.IdUnico();
            _minhas[pipelineId] = 1;
            var acao = AcaoBotEntity.Criar(app.Id, "T1", projeto.Id, 3, TipoAcaoBot.AbrirReview, 1, null, pessoa.Id, null, T0).Value;
            acao.RegistrarPipeline(pipelineId);
            acao.Iniciar(T0.AddMinutes(1));
            ctx.Add(acao);
            await ctx.SaveChangesAsync(Ct);
            return new Cenario(acao.Id, acao.PublicId, caminho, pipelineId);
        }

        private IHost NovoHost()
        {
            var gitlab = Substitute.For<IGitLab>();
            // Só as pipelines deste teste respondem; as de outros testes que dividem o banco ficam intocadas (erro transitório).
            gitlab.ObterPipelineAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(c => _minhas.ContainsKey(c.ArgAt<long>(2))
                    ? Result<PipelineGitLabDto>.Sucesso(new(c.ArgAt<long>(2), "success"))
                    : Result<PipelineGitLabDto>.Falha(new Erro("gitlab.indisponivel", "fora", TipoErro.Failure)));

            return HostDeTeste.Criar(infra, HostDeTeste.AmbienteUnico(), servicos: s =>
            {
                // O host de teste só registra o contexto de escrita; a leitura do Projeto pelo caminho usa o de leitura.
                s.AddDbContext<Esteira.Data.Common.Persistence.EsteiraReadContext>(o => o.UsarPostgres(infra.ConnectionString));
                // Webhook (o controller dá o ator "sistema") e consulta rodam sem pessoa logada: o stub entrega um Id para a auditoria.
                var usuario = Substitute.For<Esteira.Application.Interfaces.Usuario.IUsuarioAtual>();
                usuario.Id.Returns("sistema");
                s.AddSingleton(usuario);
                s.AddSingleton(gitlab);
                s.AddSingleton(Substitute.For<IDescarteDoPainel>());
                s.AddSingleton(Substitute.For<IPublicadorDeEventos>());
            });
        }

        private static string Corpo(Cenario c)
            => $$$"""{"object_attributes":{"id":{{{c.PipelineId}}},"status":"success","ref":"feature/t1-x","sha":"abc"},"project":{"path_with_namespace":"{{{c.Caminho}}}"}}""";

        private static async Task<Result> WebhookAsync(IHost host, Cenario c, Barrier largada)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var tratador = escopo.ServiceProvider.GetRequiredService<ITratadorDeWebhookGitLab>();
            largada.SignalAndWait(Ct);
            return await tratador.TratarAsync("Pipeline Hook", Corpo(c), Ct);
        }

        private static async Task ConsultaAsync(IHost host, Barrier largada)
        {
            await using var escopo = host.Services.CreateAsyncScope();
            var consulta = escopo.ServiceProvider.GetRequiredService<ConsultaDePipelines>();
            largada.SignalAndWait(Ct);
            await consulta.ExecutarAsync(Ct);
        }

        [Fact, DisplayName("6: webhook e consulta periódica concluindo a mesma Ação juntas aplicam uma conclusão e a outra é no-op")]
        public async Task Conclusao_simultanea_e_noop()
        {
            using var host = NovoHost();
            await host.StartAsync(Ct);

            for (var rodada = 0; rodada < 10; rodada++)
            {
                var c = await CriarAsync();
                var largada = new Barrier(2);

                var webhook = Task.Run(() => WebhookAsync(host, c, largada), Ct);
                var consulta = Task.Run(() => ConsultaAsync(host, largada), Ct);
                await Task.WhenAll(webhook, consulta);

                (await webhook).IsSuccess.Should().BeTrue($"rodada {rodada}");
                await using var ctx = ContextoDoCadastro.Novo(infra);
                var acao = await ctx.Set<AcaoBotEntity>().AsNoTracking().SingleAsync(a => a.Id == c.AcaoId, Ct);
                acao.Estado.Should().Be(EstadoAcaoBot.Confirmada, $"rodada {rodada}");
                acao.CodigoErro.Should().BeNull();
            }

            await host.StopAsync(Ct);
        }

        [Fact, DisplayName("6: o mesmo webhook chegando duas vezes seguidas deixa a Ação confirmada e responde sucesso nas duas")]
        public async Task Webhook_repetido_e_noop()
        {
            using var host = NovoHost();
            await host.StartAsync(Ct);
            var c = await CriarAsync();

            for (var i = 0; i < 2; i++)
            {
                await using var escopo = host.Services.CreateAsyncScope();
                var resultado = await escopo.ServiceProvider.GetRequiredService<ITratadorDeWebhookGitLab>()
                    .TratarAsync("Pipeline Hook", Corpo(c), Ct);
                resultado.IsSuccess.Should().BeTrue();
            }

            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await ctx.Set<AcaoBotEntity>().AsNoTracking().SingleAsync(a => a.Id == c.AcaoId, Ct)).Estado
                .Should().Be(EstadoAcaoBot.Confirmada);
            await host.StopAsync(Ct);
        }
    }
}
