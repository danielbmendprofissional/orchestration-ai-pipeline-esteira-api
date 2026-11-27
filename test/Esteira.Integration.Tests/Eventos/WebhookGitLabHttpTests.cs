using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.Application.Eventos;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Esteira.Integration.Tests.Aplicacoes;
using Microsoft.EntityFrameworkCore;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Esteira.Integration.Tests.Eventos
{
    // T1.3, critérios 2 e 3: POST /api/webhooks/gitlab na borda HTTP. Segredo só pelo contrato de configuração.
    [Trait("Fatia", "T1")]
    public sealed class WebhookGitLabHttpTests : IClassFixture<ApiDeTeste>
    {
        private const string Segredo = "segredo-do-webhook-8f3a91c2";
        private const string Rota = "/api/webhooks/gitlab";
        private const string CorpoPipeline =
            """{"object_attributes":{"id":1,"status":"success","ref":"main","sha":"a"},"project":{"path_with_namespace":"nao/cadastrado"}}""";

        private readonly ApiDeTeste _api;
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly InfraestruturaFixture _infra;

        public WebhookGitLabHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
        }

        private WebApplicationFactory<Program> Com(ITratadorDeWebhookGitLab? falso = null, string? segredo = Segredo)
            => _api.WithWebHostBuilder(b =>
            {
                if (segredo is not null)
                    b.UseSetting("GitLab:Webhook:Secret", segredo);
                if (falso is not null)
                    b.ConfigureTestServices(s =>
                    {
                        s.RemoveAll<ITratadorDeWebhookGitLab>();
                        s.AddSingleton(falso);
                    });
            });

        private static Task<HttpResponseMessage> PostarAsync(WebApplicationFactory<Program> api, string? token, string? evento, HttpContent corpo)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Post, Rota) { Content = corpo };
            if (token is not null)
                pedido.Headers.Add("X-Gitlab-Token", token);
            if (evento is not null)
                pedido.Headers.Add("X-Gitlab-Event", evento);
            return cliente.SendAsync(pedido, Ct);
        }

        private static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

        [Theory, DisplayName("2: token ausente, diferente ou segredo não configurado responde 401 webhook.token-invalido sem chegar ao tratador")]
        [InlineData(null, true)]
        [InlineData("outro-token", true)]
        [InlineData("", true)]
        [InlineData(Segredo, false)]
        public async Task Token_invalido_401(string? token, bool segredoConfigurado)
        {
            var tratador = Substitute.For<ITratadorDeWebhookGitLab>();
            var api = Com(tratador, segredoConfigurado ? Segredo : null);

            using var resposta = await PostarAsync(api, token, "Pipeline Hook", Json(CorpoPipeline));

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await resposta.Content.ReadAsStringAsync(Ct)).Should().Contain("webhook.token-invalido");
            await tratador.DidNotReceiveWithAnyArgs().TratarAsync(default, default!, default);
        }

        [Fact, DisplayName("2: o segredo e o token enviado não aparecem na resposta nem no log")]
        public async Task Segredo_fora_de_log_e_resposta()
        {
            var tratador = Substitute.For<ITratadorDeWebhookGitLab>();
            tratador.TratarAsync(default, default!, default).ReturnsForAnyArgs(Result.Sucesso());
            var api = Com(tratador);
            const string errado = "token-errado-77bd01aa";

            using var recusada = await PostarAsync(api, errado, "Pipeline Hook", Json(CorpoPipeline));
            using var aceita = await PostarAsync(api, Segredo, "Pipeline Hook", Json(CorpoPipeline));

            var textos = string.Concat(await recusada.Content.ReadAsStringAsync(Ct), await aceita.Content.ReadAsStringAsync(Ct));
            textos.Should().NotContain(Segredo).And.NotContain(errado);
            var log = api.Services.GetRequiredService<LogDeTeste>().Eventos
                .Select(e => e.RenderMessage() + string.Concat(e.Properties.Values.Select(v => v.ToString()))).ToList();
            log.Should().NotContain(l => l.Contains(Segredo) || l.Contains(errado) || l.Contains("nao/cadastrado"));
        }

        [Fact, DisplayName("3: Projeto não cadastrado responde 200 sem efeito")]
        public async Task Projeto_desconhecido_200()
        {
            var api = Com();

            using var resposta = await PostarAsync(api, Segredo, "Pipeline Hook", Json(CorpoPipeline));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact, DisplayName("3: evento que a Esteira não trata responde 200 e nem chega a consultar o Projeto")]
        public async Task Evento_nao_tratado_200()
        {
            var api = Com();

            using var resposta = await PostarAsync(api, Segredo, "Push Hook", Json(CorpoPipeline));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact, DisplayName("3: corpo acima de 1 MB responde 413 sem chegar ao tratador")]
        public async Task Corpo_grande_413()
        {
            var tratador = Substitute.For<ITratadorDeWebhookGitLab>();
            var api = Com(tratador);
            var grande = new string('x', 1024 * 1024 + 1);

            using var resposta = await PostarAsync(api, Segredo, "Pipeline Hook", Json(grande));

            resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
            await tratador.DidNotReceiveWithAnyArgs().TratarAsync(default, default!, default);
        }

        [Fact, DisplayName("3: corpo de exatamente 1 MB ainda é lido")]
        public async Task Corpo_no_limite_passa()
        {
            var tratador = Substitute.For<ITratadorDeWebhookGitLab>();
            tratador.TratarAsync(default, default!, default).ReturnsForAnyArgs(Result.Sucesso());
            var api = Com(tratador);

            using var resposta = await PostarAsync(api, Segredo, "Pipeline Hook", Json(new string('x', 1024 * 1024)));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            await tratador.Received(1).TratarAsync("Pipeline Hook", Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Corpo que não é JSON responde 400 webhook.corpo-invalido")]
        public async Task Corpo_ilegivel_400()
        {
            var api = Com();

            using var resposta = await PostarAsync(api, Segredo, "Pipeline Hook", Json("isto não é json"));

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await resposta.Content.ReadAsStringAsync(Ct)).Should().Contain("webhook.corpo-invalido");
        }

        [Fact, DisplayName("1: Pipeline Hook com o token certo confirma a Ação abrir-review em executando (rota anônima, ator sistema na auditoria) e o repetido é no-op")]
        public async Task Pipeline_hook_confirma_a_acao_pela_rota()
        {
            long acaoId;
            string caminho = $"g/{Guid.NewGuid():N}";
            long pipelineId = ContextoDoCadastro.IdUnico();
            await using (var ctx = ContextoDoCadastro.Novo(_infra))
            {
                var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
                ctx.Add(app);
                await ctx.SaveChangesAsync(Ct);
                var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), caminho, "front", Stack.Angular);
                var pessoa = PessoaEntity.Criar(Guid.NewGuid().ToString(), "Ana", "a@x.com", DateTime.UtcNow);
                ctx.AddRange(projeto, pessoa);
                await ctx.SaveChangesAsync(Ct);
                var acao = AcaoBotEntity.Criar(app.Id, "T1", projeto.Id, 3, TipoAcaoBot.AbrirReview, 1, null, pessoa.Id, null, DateTime.UtcNow).Value;
                acao.RegistrarPipeline(pipelineId);
                acao.Iniciar(DateTime.UtcNow);
                ctx.Add(acao);
                await ctx.SaveChangesAsync(Ct);
                acaoId = acao.Id;
            }
            var corpo = $$$"""{"object_attributes":{"id":{{{pipelineId}}},"status":"success","ref":"feature/t1-x","sha":"abc"},"project":{"path_with_namespace":"{{{caminho}}}"}}""";
            var api = Com();

            using var primeira = await PostarAsync(api, Segredo, "Pipeline Hook", Json(corpo));
            using var repetida = await PostarAsync(api, Segredo, "Pipeline Hook", Json(corpo));

            primeira.StatusCode.Should().Be(HttpStatusCode.OK);
            repetida.StatusCode.Should().Be(HttpStatusCode.OK);
            await using var leitura = ContextoDoCadastro.Novo(_infra);
            (await leitura.Set<AcaoBotEntity>().AsNoTracking().SingleAsync(a => a.Id == acaoId, Ct)).Estado.Should().Be(EstadoAcaoBot.Confirmada);
            (await leitura.Set<RegistroAuditoriaEntity>().AsNoTracking().AnyAsync(r => r.Usuario == "webhook-gitlab", Ct)).Should().BeTrue();
        }
    }
}
