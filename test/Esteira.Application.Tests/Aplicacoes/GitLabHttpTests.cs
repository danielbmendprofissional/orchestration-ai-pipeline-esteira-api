using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.3, critério 4 (e base do 5): adaptador IGitLab contra um HttpMessageHandler falso. Só leitura; token nunca vaza.
    [Trait("Fatia", "A1")]
    public class GitLabHttpTests
    {
        private const string Token = "glpat-SEGREDO-123";
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

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo, params (string, string)[] headers)
        {
            var r = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
            foreach (var (k, v) in headers) r.Headers.Add(k, v);
            return r;
        }

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

        [Fact, DisplayName("ObterGrupo 200: devolve ID, caminho e nome; envia o token do grupo no header e só faz GET")]
        public async Task ObterGrupo_200()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, """{"id":42,"full_path":"valuto","name":"Valuto"}"""));

            var r = await gitlab.ObterGrupoAsync("valuto", Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().Be(new Esteira.Application.Interfaces.Integracoes.GrupoGitLabDto(42, "valuto", "Valuto"));
            var pedido = handler.Pedidos.Should().ContainSingle().Subject;
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/groups/valuto");
            pedido.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle().Which.Should().Be(Token);
        }

        [Fact, DisplayName("ObterGrupo com subgrupo: o caminho vai codificado (a%2Fb)")]
        public async Task ObterGrupo_Subgrupo()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, """{"id":1,"full_path":"valuto/sub","name":"Sub"}"""));
            // o token é do grupo raiz da configuração; subgrupo usa o caminho completo configurado
            var opcoes = new GitLabOptions();
            opcoes.Bots["valuto/sub"] = new GitLabBotOptions { Token = Token };
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://gitlab.exemplo/") };

            await new GitLabHttp(http, Options.Create(opcoes), new FakeLogger<GitLabHttp>()).ObterGrupoAsync("valuto/sub", Ct);

            handler.Pedidos.Single().RequestUri!.OriginalString.Should().EndWith("/api/v4/groups/valuto%2Fsub");
        }

        [Theory, DisplayName("ObterGrupo 404 ou 403: gitlab.grupo-nao-encontrado")]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task ObterGrupo_NaoEncontrado(HttpStatusCode status)
        {
            var (gitlab, _, _) = Criar(_ => Json(status, """{"message":"404 Group Not Found"}"""));

            var r = await gitlab.ObterGrupoAsync("valuto", Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.grupo-nao-encontrado");
        }

        [Fact, DisplayName("Sem token para o grupo: gitlab.bot-nao-configurado, sem chamar o GitLab")]
        public async Task SemToken()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, "{}"), comToken: false);

            var r = await gitlab.ObterGrupoAsync("valuto", Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
            handler.Pedidos.Should().BeEmpty();
        }

        [Fact, DisplayName("Token rejeitado (401): gitlab.bot-nao-configurado")]
        public async Task TokenRejeitado()
        {
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.Unauthorized, """{"message":"401 Unauthorized"}"""));

            var r = await gitlab.ObterGrupoAsync("valuto", Ct);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("gitlab.bot-nao-configurado");
        }

        [Fact, DisplayName("GitLab fora do ar (5xx ou exceção): gitlab.indisponivel, e o token não aparece em erro nem em log")]
        public async Task Indisponivel_TokenNaoVaza()
        {
            var (g5xx, _, log5xx) = Criar(_ => Json(HttpStatusCode.InternalServerError, "erro"));
            var (gEx, _, logEx) = Criar(_ => throw new HttpRequestException($"falha com {Token}"));

            var r1 = await g5xx.ObterGrupoAsync("valuto", Ct);
            var r2 = await gEx.ObterGrupoAsync("valuto", Ct);

            foreach (var r in new[] { r1, r2 })
            {
                var erro = r.Errors.Should().ContainSingle().Subject;
                erro.Code.Should().Be("gitlab.indisponivel");
                (erro.Code + erro.Message).Should().NotContain(Token);
            }

            foreach (var log in new[] { log5xx, logEx })
                log.Collector.GetSnapshot().Select(l => l.Message + l.Exception).Should().NotContain(m => m.Contains(Token));
        }

        [Fact, DisplayName("ListarProjetosDoGrupo: percorre as páginas pelo X-Next-Page")]
        public async Task ListarProjetos_Paginas()
        {
            var (gitlab, handler, _) = Criar(req => req.RequestUri!.Query.Contains("page=2")
                ? Json(HttpStatusCode.OK, """[{"id":11,"path_with_namespace":"valuto/b","name":"B"}]""")
                : Json(HttpStatusCode.OK, """[{"id":10,"path_with_namespace":"valuto/a","name":"A"}]""", ("X-Next-Page", "2")));

            var r = await gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Ct);

            r.Value.Select(p => (p.Id, p.Caminho, p.Nome)).Should().Equal((10L, "valuto/a", "A"), (11L, "valuto/b", "B"));
            handler.Pedidos.Should().OnlyContain(p => p.RequestUri!.AbsolutePath == "/api/v4/groups/42/projects");
        }

        [Fact, DisplayName("ObterArquivo: lê o raw na ref; 404 vira sucesso com null")]
        public async Task ObterArquivo()
        {
            var (gitlab, handler, _) = Criar(req => req.RequestUri!.OriginalString.Contains("ausente")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("APP_TECHNOLOGY: dotnet") });

            var existente = await gitlab.ObterArquivoAsync("valuto", 10, ".gitlab-ci.yml", "main", Ct);
            var ausente = await gitlab.ObterArquivoAsync("valuto", 10, "ausente.yml", "main", Ct);

            existente.Value.Should().Be("APP_TECHNOLOGY: dotnet");
            ausente.IsSuccess.Should().BeTrue();
            ausente.Value.Should().BeNull();
            handler.Pedidos[0].RequestUri!.OriginalString.Should().Contain("/api/v4/projects/10/repository/files/.gitlab-ci.yml/raw").And.Contain("ref=main");
        }
    }
}
