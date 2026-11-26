using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Esteira.Application.Interfaces.Integracoes;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // P2b.1 (critérios 4 e 17 da P2b): a escrita no GitLab pela porta IGitLab. Só o formato de cada chamada e o
    // mapeamento de status para erro; o GitLab é falso e nenhum token pode aparecer em erro, log ou retorno.
    [Trait("Fatia", "P2b")]
    public class GitLabHttpEscritaTests
    {
        private const string Token = "glpat-BOT-SEGREDO-123";
        private const string TokenDeTrigger = "trg-TRIGGER-SEGREDO-456";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Pedidos { get; } = [];

            public List<string> Corpos { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Pedidos.Add(request);
                Corpos.Add(request.Content is null
                    ? string.Empty
                    : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
                return Task.FromResult(responder(request));
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo)
            => new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

        private static (GitLabHttp Gitlab, HandlerFalso Handler, FakeLogger<GitLabHttp> Log) Criar(
            Func<HttpRequestMessage, HttpResponseMessage> responder, bool comToken = true)
        {
            var handler = new HandlerFalso(responder);
            var opcoes = new GitLabOptions();
            if (comToken)
                opcoes.Bots["valuto"] = new GitLabBotOptions { Token = Token };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.exemplo/") };
            var log = new FakeLogger<GitLabHttp>();
            return (new GitLabHttp(http, Options.Create(opcoes), log), handler, log);
        }

        [Fact, DisplayName("ObterMergeRequest 200: GET na MR por iid devolve estado, branch, SHA e status da pipeline da cabeça")]
        public async Task ObterMergeRequest_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK,
                """{"iid":7,"state":"opened","source_branch":"feature/x","sha":"abc123","head_pipeline":{"status":"success"}}"""));

            var r = await gitlab.ObterMergeRequestAsync("valuto", 10, 7, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().Be(new MergeRequestDaAcaoGitLabDto(7, "opened", "feature/x", "abc123", "success"));
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7");
            pedido.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(Token);
        }

        [Fact, DisplayName("ObterMergeRequest sem head_pipeline: status da pipeline nulo")]
        public async Task ObterMergeRequest_SemPipeline()
        {
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.OK,
                """{"iid":7,"state":"opened","source_branch":"feature/x","sha":"abc123"}"""));

            var r = await gitlab.ObterMergeRequestAsync("valuto", 10, 7, Ct);

            r.Value.StatusPipeline.Should().BeNull();
        }

        [Fact, DisplayName("Comentar 201: POST em /notes com o corpo em JSON e devolve o id da nota")]
        public async Task Comentar_201()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.Created, """{"id":999}"""));

            var r = await gitlab.ComentarMergeRequestAsync("valuto", 10, 7, "texto do humano", Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().Be(999);
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Post);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7/notes");
            pedido.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            handler.Corpos.Single().Should().Be("""{"body":"texto do humano"}""");
        }

        [Fact, DisplayName("Fechar 200: PUT na MR com state_event=close e devolve o estado closed")]
        public async Task Fechar_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK,
                """{"iid":7,"state":"closed","source_branch":"f","sha":"a"}"""));

            var r = await gitlab.FecharMergeRequestAsync("valuto", 10, 7, Ct);

            r.Value.Estado.Should().Be("closed");
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Put);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7");
            handler.Corpos.Single().Should().Be("""{"state_event":"close"}""");
        }

        [Fact, DisplayName("Reabrir 200: PUT na MR com state_event=reopen e devolve o estado opened")]
        public async Task Reabrir_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK,
                """{"iid":7,"state":"opened","source_branch":"f","sha":"a"}"""));

            var r = await gitlab.ReabrirMergeRequestAsync("valuto", 10, 7, Ct);

            r.Value.Estado.Should().Be("opened");
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Put);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7");
            handler.Corpos.Single().Should().Be("""{"state_event":"reopen"}""");
        }

        [Fact, DisplayName("Mergear 200: PUT em /merge com merge commit, sem squash e sem apagar a branch de origem")]
        public async Task Mergear_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK,
                """{"iid":7,"state":"merged","source_branch":"f","sha":"a"}"""));

            var r = await gitlab.MergearMergeRequestAsync("valuto", 10, 7, Ct);

            r.Value.Estado.Should().Be("merged");
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Put);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7/merge");
            handler.Corpos.Single().Should().Be("""{"squash":false,"should_remove_source_branch":false}""");
        }

        [Fact, DisplayName("CriarTriggerToken 201: POST em /triggers com a descrição e devolve id e token")]
        public async Task CriarTriggerToken_201()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.Created, """{"id":55,"token":"trg-abc"}"""));

            var r = await gitlab.CriarTriggerTokenAsync("valuto", 10, "esteira-acao-1:2", Ct);

            r.Value.Should().Be(new TriggerTokenGitLabDto(55, "trg-abc"));
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Post);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/triggers");
            handler.Corpos.Single().Should().Be("""{"description":"esteira-acao-1:2"}""");
        }

        [Fact, DisplayName("ApagarTriggerToken 204: DELETE em /triggers/{id}")]
        public async Task ApagarTriggerToken_204()
        {
            var (gitlab, handler, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

            var r = await gitlab.ApagarTriggerTokenAsync("valuto", 10, 55, Ct);

            r.IsSuccess.Should().BeTrue();
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Delete);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/triggers/55");
        }

        [Fact, DisplayName("ApagarTriggerToken 404: o token já não existe, então é sucesso (apagar é idempotente)")]
        public async Task ApagarTriggerToken_404_Idempotente()
        {
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.NotFound, """{"message":"404 Not found"}"""));

            var r = await gitlab.ApagarTriggerTokenAsync("valuto", 10, 55, Ct);

            r.IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("CriarPipelinePorTrigger 201: POST em /trigger/pipeline com token, ref e as duas variáveis, devolve o id")]
        public async Task CriarPipelinePorTrigger_201()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.Created, """{"id":1234}"""));

            var variaveis = new Dictionary<string, string>
            {
                ["PIPELINE_PROCESS"] = "preview",
                ["PREVIEW_FATIA"] = "p2b",
            };
            var r = await gitlab.CriarPipelinePorTriggerAsync("valuto", 10, TokenDeTrigger, "feature/x", variaveis, Ct);

            r.Value.Should().Be(1234);
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Post);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/trigger/pipeline");
            pedido.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
            // o trigger token autentica o disparo pelo corpo; o token do bot não é necessário nesta chamada
            pedido.Headers.Contains("PRIVATE-TOKEN").Should().BeFalse();
            var corpo = Uri.UnescapeDataString(handler.Corpos.Single());
            corpo.Should().Contain($"token={TokenDeTrigger}")
                .And.Contain("ref=feature/x")
                .And.Contain("variables[PIPELINE_PROCESS]=preview")
                .And.Contain("variables[PREVIEW_FATIA]=p2b");
        }

        [Fact, DisplayName("ObterPipeline 200: GET na pipeline por id devolve o status")]
        public async Task ObterPipeline_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, """{"id":1234,"status":"success"}"""));

            var r = await gitlab.ObterPipelineAsync("valuto", 10, 1234, Ct);

            r.Value.Should().Be(new PipelineGitLabDto(1234, "success"));
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/pipelines/1234");
        }

        [Theory, DisplayName("MR 401/403: gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task ObterMergeRequest_SemPermissao(HttpStatusCode status)
        {
            var (gitlab, _, _) = Criar(_ => Json(status, """{"message":"no"}"""));

            var r = await gitlab.ObterMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.sem-permissao");
        }

        [Fact, DisplayName("MR 404: gitlab.mr-nao-encontrada")]
        public async Task ObterMergeRequest_404()
        {
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.NotFound, """{"message":"404 Not found"}"""));

            var r = await gitlab.ObterMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.mr-nao-encontrada");
        }

        [Theory, DisplayName("Merge recusado (409/406/405): gitlab.merge-recusado")]
        [InlineData(HttpStatusCode.Conflict)]
        [InlineData(HttpStatusCode.NotAcceptable)]
        [InlineData(HttpStatusCode.MethodNotAllowed)]
        public async Task Mergear_Recusado(HttpStatusCode status)
        {
            var (gitlab, _, _) = Criar(_ => Json(status, """{"message":"405 Method Not Allowed"}"""));

            var r = await gitlab.MergearMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.merge-recusado");
        }

        [Theory, DisplayName("Escrita com 5xx ou 429: gitlab.indisponivel")]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task Escrita_Indisponivel(HttpStatusCode status)
        {
            var (gitlab, _, _) = Criar(_ => Json(status, "erro"));

            var r = await gitlab.ObterPipelineAsync("valuto", 10, 1234, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }

        [Fact, DisplayName("Endpoint de token/pipeline com 4xx definitivo cai no erro mais próximo (gitlab.indisponivel) — ver Suposições")]
        public async Task CriarPipeline_4xx_Indisponivel()
        {
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.BadRequest, """{"message":"bad"}"""));

            var r = await gitlab.CriarPipelinePorTriggerAsync(
                "valuto", 10, TokenDeTrigger, "feature/x", new Dictionary<string, string>(), Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }

        [Fact, DisplayName("Sem token do grupo: gitlab.bot-nao-configurado, sem chamar o GitLab")]
        public async Task SemToken()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, "{}"), comToken: false);

            var r = await gitlab.ObterMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
            handler.Pedidos.Should().BeEmpty();
        }

        [Fact, DisplayName("Token do bot não aparece em erro nem em log (critério 17), mesmo quando a exceção o carrega")]
        public async Task TokenDoBot_NaoVaza()
        {
            var (g5xx, _, log5xx) = Criar(_ => Json(HttpStatusCode.InternalServerError, "x"));
            var (gEx, _, logEx) = Criar(_ => throw new HttpRequestException($"falha com {Token}"));

            var r1 = await g5xx.MergearMergeRequestAsync("valuto", 10, 7, Ct);
            var r2 = await gEx.MergearMergeRequestAsync("valuto", 10, 7, Ct);

            foreach (var r in new[] { r1, r2 })
            {
                var erro = r.Errors.Should().ContainSingle().Subject;
                erro.Code.Should().Be("gitlab.indisponivel");
                (erro.Code + erro.Message).Should().NotContain(Token);
            }

            foreach (var log in new[] { log5xx, logEx })
                log.Collector.GetSnapshot().Select(l => l.Message + l.Exception)
                    .Should().NotContain(m => m!.Contains(Token));
        }

        [Fact, DisplayName("Token de trigger não aparece em erro nem em log (critério 17)")]
        public async Task TokenDeTrigger_NaoVaza()
        {
            var (gitlab, _, log) = Criar(_ => Json(HttpStatusCode.InternalServerError, "x"));

            var r = await gitlab.CriarPipelinePorTriggerAsync(
                "valuto", 10, TokenDeTrigger, "feature/x", new Dictionary<string, string> { ["PIPELINE_PROCESS"] = "preview" }, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
            (r.Errors.Single().Code + r.Errors.Single().Message).Should().NotContain(TokenDeTrigger);
            log.Collector.GetSnapshot().Select(l => l.Message + l.Exception)
                .Should().NotContain(m => m!.Contains(TokenDeTrigger));
        }

        [Fact, DisplayName("O token recém-criado do trigger não é escrito em log (critério 17)")]
        public async Task TriggerCriado_NaoVaiParaLog()
        {
            var (gitlab, _, log) = Criar(_ => Json(HttpStatusCode.Created, """{"id":55,"token":"trg-NOVO-SEGREDO"}"""));

            var r = await gitlab.CriarTriggerTokenAsync("valuto", 10, "esteira-acao-1:2", Ct);

            r.Value.Token.Should().Be("trg-NOVO-SEGREDO");
            log.Collector.GetSnapshot().Select(l => l.Message + l.Exception)
                .Should().NotContain(m => m!.Contains("trg-NOVO-SEGREDO"));
        }
    }
}
