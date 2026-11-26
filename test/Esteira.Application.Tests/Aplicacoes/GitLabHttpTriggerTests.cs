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
    // P2b.3: os dois métodos novos da porta (listar trigger tokens e cancelar pipeline) no Adaptador HTTP.
    // Só o formato da chamada e o mapeamento de status para erro; o GitLab é falso.
    [Trait("Fatia", "P2b")]
    public class GitLabHttpTriggerTests
    {
        private const string Token = "glpat-BOT-SEGREDO-123";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Pedidos { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Pedidos.Add(request);
                return Task.FromResult(responder(request));
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo)
            => new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

        private static (GitLabHttp Gitlab, HandlerFalso Handler) Criar(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handler = new HandlerFalso(responder);
            var opcoes = new GitLabOptions();
            opcoes.Bots["valuto"] = new GitLabBotOptions { Token = Token };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.exemplo/") };
            return (new GitLabHttp(http, Options.Create(opcoes), new FakeLogger<GitLabHttp>()), handler);
        }

        [Fact, DisplayName("ListarTriggerTokens 200: GET paginado em /triggers e devolve id, descrição e created_at")]
        public async Task ListarTriggerTokens_200()
        {
            var (gitlab, handler) = Criar(_ => Json(HttpStatusCode.OK,
                """[{"id":55,"description":"esteira-acao-1:2","created_at":"2026-11-23T12:00:00Z"},{"id":56,"description":"outro","created_at":"2026-11-22T09:30:00Z"}]"""));

            var r = await gitlab.ListarTriggerTokensAsync("valuto", 10, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().HaveCount(2);
            r.Value[0].Should().Be(new TriggerTokenListadoGitLabDto(55, "esteira-acao-1:2", DateTimeOffset.Parse("2026-11-23T12:00:00Z")));
            r.Value[1].Id.Should().Be(56);
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/triggers");
            pedido.RequestUri.Query.Should().Contain("per_page=100").And.Contain("page=1");
            pedido.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(Token);
        }

        [Fact, DisplayName("ListarTriggerTokens segue X-Next-Page e junta as páginas")]
        public async Task ListarTriggerTokens_Paginado()
        {
            var chamadas = 0;
            var (gitlab, handler) = Criar(req =>
            {
                chamadas++;
                var pagina = req.RequestUri!.Query.Contains("page=2") ? 2 : 1;
                var resposta = Json(HttpStatusCode.OK, pagina == 1
                    ? """[{"id":1,"description":"a","created_at":"2026-11-23T12:00:00Z"}]"""
                    : """[{"id":2,"description":"b","created_at":"2026-11-23T12:01:00Z"}]""");
                resposta.Headers.Add("X-Next-Page", pagina == 1 ? "2" : string.Empty);
                return resposta;
            });

            var r = await gitlab.ListarTriggerTokensAsync("valuto", 10, Ct);

            r.Value.Select(t => t.Id).Should().Equal(1, 2);
            chamadas.Should().Be(2);
            handler.Pedidos.Should().HaveCount(2);
        }

        [Theory, DisplayName("ListarTriggerTokens 401/403: gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task ListarTriggerTokens_SemPermissao(HttpStatusCode status)
        {
            var (gitlab, _) = Criar(_ => Json(status, """{"message":"no"}"""));

            var r = await gitlab.ListarTriggerTokensAsync("valuto", 10, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.sem-permissao");
        }

        [Fact, DisplayName("ListarTriggerTokens 5xx: gitlab.indisponivel")]
        public async Task ListarTriggerTokens_Indisponivel()
        {
            var (gitlab, _) = Criar(_ => Json(HttpStatusCode.InternalServerError, "erro"));

            var r = await gitlab.ListarTriggerTokensAsync("valuto", 10, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }

        [Fact, DisplayName("CancelarPipeline 200: POST em /pipelines/{id}/cancel devolve o status")]
        public async Task CancelarPipeline_200()
        {
            var (gitlab, handler) = Criar(_ => Json(HttpStatusCode.OK, """{"id":1234,"status":"canceled"}"""));

            var r = await gitlab.CancelarPipelineAsync("valuto", 10, 1234, Ct);

            r.Value.Should().Be(new PipelineGitLabDto(1234, "canceled"));
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Post);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/pipelines/1234/cancel");
            pedido.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(Token);
        }

        [Theory, DisplayName("CancelarPipeline 401/403: gitlab.sem-permissao; 5xx/404: gitlab.indisponivel")]
        [InlineData(HttpStatusCode.Unauthorized, "gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.Forbidden, "gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.NotFound, "gitlab.indisponivel")]
        [InlineData(HttpStatusCode.InternalServerError, "gitlab.indisponivel")]
        public async Task CancelarPipeline_Erros(HttpStatusCode status, string codigo)
        {
            var (gitlab, _) = Criar(_ => Json(status, """{"message":"erro"}"""));

            var r = await gitlab.CancelarPipelineAsync("valuto", 10, 1234, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be(codigo);
        }
    }
}
