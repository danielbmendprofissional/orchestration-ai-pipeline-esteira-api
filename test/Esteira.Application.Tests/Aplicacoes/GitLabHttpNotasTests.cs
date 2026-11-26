using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // P2b.2, critério 10: ler as notas da MR para o marcador de idempotência. Só o formato da chamada, a paginação e o
    // mapeamento de status para erro; o GitLab é falso.
    [Trait("Fatia", "P2b")]
    public class GitLabHttpNotasTests
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

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo, string proxima = null)
        {
            var mensagem = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
            if (proxima is not null)
                mensagem.Headers.Add("X-Next-Page", proxima);
            return mensagem;
        }

        private static (GitLabHttp Gitlab, HandlerFalso Handler) Criar(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handler = new HandlerFalso(responder);
            var opcoes = new GitLabOptions();
            opcoes.Bots["valuto"] = new GitLabBotOptions { Token = Token };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.exemplo/") };
            return (new GitLabHttp(http, Options.Create(opcoes), new FakeLogger<GitLabHttp>()), handler);
        }

        [Fact, DisplayName("GET /notes 200: devolve o corpo de cada nota da MR")]
        public async Task ListarNotas_200()
        {
            var (gitlab, handler) = Criar(_ => Json(HttpStatusCode.OK,
                """[{"id":1,"body":"primeira nota"},{"id":2,"body":"<!-- esteira-acao:5:P2:aprovar:71:3:1 -->"}]"""));

            var r = await gitlab.ListarNotasMergeRequestAsync("valuto", 10, 7, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().Equal("primeira nota", "<!-- esteira-acao:5:P2:aprovar:71:3:1 -->");
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/10/merge_requests/7/notes");
            pedido.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(Token);
        }

        [Fact, DisplayName("Paginação: segue o X-Next-Page e junta as notas das duas páginas")]
        public async Task ListarNotas_Paginado()
        {
            var (gitlab, handler) = Criar(pedido => pedido.RequestUri!.Query.Contains("page=2")
                ? Json(HttpStatusCode.OK, """[{"body":"segunda"}]""")
                : Json(HttpStatusCode.OK, """[{"body":"primeira"}]""", proxima: "2"));

            var r = await gitlab.ListarNotasMergeRequestAsync("valuto", 10, 7, Ct);

            r.Value.Should().Equal("primeira", "segunda");
            handler.Pedidos.Should().HaveCount(2);
            handler.Pedidos[1].RequestUri!.Query.Should().Contain("page=2");
        }

        [Fact, DisplayName("Notas 404: gitlab.mr-nao-encontrada")]
        public async Task ListarNotas_404()
        {
            var (gitlab, _) = Criar(_ => Json(HttpStatusCode.NotFound, """{"message":"404 Not found"}"""));

            var r = await gitlab.ListarNotasMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.mr-nao-encontrada");
        }

        [Theory, DisplayName("Notas 401/403: gitlab.sem-permissao")]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task ListarNotas_SemPermissao(HttpStatusCode status)
        {
            var (gitlab, _) = Criar(_ => Json(status, """{"message":"no"}"""));

            var r = await gitlab.ListarNotasMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.sem-permissao");
        }

        [Theory, DisplayName("Notas 5xx/429: gitlab.indisponivel")]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task ListarNotas_Indisponivel(HttpStatusCode status)
        {
            var (gitlab, _) = Criar(_ => Json(status, "erro"));

            var r = await gitlab.ListarNotasMergeRequestAsync("valuto", 10, 7, Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.indisponivel");
        }
    }
}
