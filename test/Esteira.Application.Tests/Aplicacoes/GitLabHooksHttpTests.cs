using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Esteira.Application.Interfaces.Integracoes;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // T1.4: adaptador dos hooks do GitLab (listar, criar, atualizar). O GitLab é falso; o segredo vai só no corpo.
    [Trait("Fatia", "T1")]
    public class GitLabHooksHttpTests
    {
        private const string TokenDoBot = "glpat-BOT-SEGREDO-123";
        private const string Segredo = "segredo-do-webhook-xyz";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly HookParaRegistrar Hook = new("https://esteira.exemplo/api/webhooks/gitlab", Segredo);

        private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Pedidos { get; } = [];
            public List<string> Corpos { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Pedidos.Add(request);
                Corpos.Add(request.Content is null ? string.Empty : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
                return Task.FromResult(responder(request));
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo)
            => new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

        private static (GitLabHooksHttp Gitlab, HandlerFalso Handler, FakeLogger<GitLabHooksHttp> Log) Criar(
            Func<HttpRequestMessage, HttpResponseMessage> responder, bool comToken = true)
        {
            var handler = new HandlerFalso(responder);
            var opcoes = new GitLabOptions();
            if (comToken)
                opcoes.Bots["valuto"] = new GitLabBotOptions { Token = TokenDoBot };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.exemplo/") };
            var log = new FakeLogger<GitLabHooksHttp>();
            return (new GitLabHooksHttp(http, Options.Create(opcoes), log), handler, log);
        }

        [Fact, DisplayName("Listar: GET paginado em /hooks devolve id e url, com o token do bot")]
        public async Task Listar()
        {
            var chamadas = 0;
            var (gitlab, handler, _) = Criar(req =>
            {
                chamadas++;
                var primeira = req.RequestUri!.Query.EndsWith("&page=1");
                var r = Json(HttpStatusCode.OK, primeira
                    ? """[{"id":1,"url":"https://a/x","token":null}]"""
                    : """[{"id":2,"url":"https://b/y"}]""");
                r.Headers.Add("X-Next-Page", primeira ? "2" : string.Empty);
                return r;
            });

            var r = await gitlab.ListarAsync("valuto", 10, Ct);

            r.Value.Should().Equal(new HookGitLabDto(1, "https://a/x"), new HookGitLabDto(2, "https://b/y"));
            chamadas.Should().Be(2);
            handler.Pedidos[0].Method.Should().Be(HttpMethod.Get);
            handler.Pedidos[0].RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/hooks");
            handler.Pedidos[0].Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(TokenDoBot);
        }

        [Fact, DisplayName("Criar: POST em /hooks com url, token, pipeline e MR ligados, push desligado e SSL verificado; devolve o id")]
        public async Task Criar_Post()
        {
            var (gitlab, handler, log) = Criar(_ => Json(HttpStatusCode.Created, """{"id":900,"url":"x"}"""));

            var r = await gitlab.CriarAsync("valuto", 10, Hook, Ct);

            r.Value.Should().Be(900);
            handler.Pedidos.Should().ContainSingle().Which.Method.Should().Be(HttpMethod.Post);
            handler.Pedidos[0].RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/hooks");
            using var corpo = JsonDocument.Parse(handler.Corpos[0]);
            var raiz = corpo.RootElement;
            (raiz.GetProperty("url").GetString(), raiz.GetProperty("token").GetString()).Should().Be((Hook.Url, Segredo));
            (raiz.GetProperty("pipeline_events").GetBoolean(), raiz.GetProperty("merge_requests_events").GetBoolean(),
                raiz.GetProperty("push_events").GetBoolean(), raiz.GetProperty("enable_ssl_verification").GetBoolean())
                .Should().Be((true, true, false, true));
            log.Collector.GetSnapshot().Select(l => l.Message).Should().NotContain(m => m.Contains(Segredo) || m.Contains(TokenDoBot));
        }

        [Fact, DisplayName("Atualizar: PUT em /hooks/{id} com o mesmo corpo")]
        public async Task Atualizar_Put()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, """{"id":77}"""));

            var r = await gitlab.AtualizarAsync("valuto", 10, 77, Hook, Ct);

            r.IsSuccess.Should().BeTrue();
            handler.Pedidos.Should().ContainSingle().Which.Method.Should().Be(HttpMethod.Put);
            handler.Pedidos[0].RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/hooks/77");
            handler.Corpos[0].Should().Contain(Segredo);
        }

        [Theory, DisplayName("Erros: 401/403 sem-permissao; 404/500/429 indisponivel; erro nunca carrega o segredo")]
        [InlineData(HttpStatusCode.Unauthorized, "gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.Forbidden, "gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.NotFound, "gitlab.indisponivel")]
        [InlineData(HttpStatusCode.InternalServerError, "gitlab.indisponivel")]
        [InlineData(HttpStatusCode.TooManyRequests, "gitlab.indisponivel")]
        public async Task Erros(HttpStatusCode status, string codigo)
        {
            var (gitlab, _, _) = Criar(_ => Json(status, "{}"));

            var listar = await gitlab.ListarAsync("valuto", 10, Ct);
            var criar = await gitlab.CriarAsync("valuto", 10, Hook, Ct);
            var atualizar = await gitlab.AtualizarAsync("valuto", 10, 1, Hook, Ct);

            listar.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            criar.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            atualizar.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
            criar.Errors[0].Message.Should().NotContain(Segredo);
        }

        [Fact, DisplayName("Sem token do bot do grupo: gitlab.bot-nao-configurado, sem chamar a rede")]
        public async Task SemBot()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, "[]"), comToken: false);

            var r = await gitlab.ListarAsync("valuto", 10, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
            handler.Pedidos.Should().BeEmpty();
        }

        [Fact, DisplayName("Falha de rede vira gitlab.indisponivel")]
        public async Task Rede()
        {
            var (gitlab, _, _) = Criar(_ => throw new HttpRequestException("boom"));

            var r = await gitlab.CriarAsync("valuto", 10, Hook, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }
    }
}
