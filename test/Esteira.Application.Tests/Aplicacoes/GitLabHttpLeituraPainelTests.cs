using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // P1.3: ListarIssues, ListarMergeRequests e ObterProjeto do adaptador IGitLab contra HttpMessageHandler falso.
    [Trait("Fatia", "P1")]
    public class GitLabHttpLeituraPainelTests
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

        [Fact, DisplayName("ListarIssues: abertas e fechadas, páginas de 100 pelo X-Next-Page, só GET com token no header")]
        public async Task ListarIssues_PaginaEMapeia()
        {
            var (gitlab, handler, _) = Criar(req => req.RequestUri!.Query.Contains("page=2")
                ? Json(HttpStatusCode.OK, """[{"iid":8,"title":"B2 — Fechada","state":"closed","labels":[]}]""")
                : Json(HttpStatusCode.OK, """[{"iid":7,"title":"B2 — Mãe","state":"opened","labels":["em-execucao","backend"]}]""", ("X-Next-Page", "2")));

            var r = await gitlab.ListarIssuesAsync("valuto", 5, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Select(i => (i.Iid, i.Titulo, i.Estado)).Should().Equal((7L, "B2 — Mãe", "opened"), (8L, "B2 — Fechada", "closed"));
            r.Value[0].Labels.Should().Equal("em-execucao", "backend");
            handler.Pedidos.Should().OnlyContain(p => p.Method == HttpMethod.Get
                && p.RequestUri!.AbsolutePath == "/api/v4/projects/5/issues"
                && p.RequestUri.Query.Contains("state=all") && p.RequestUri.Query.Contains("per_page=100")
                && p.Headers.GetValues("PRIVATE-TOKEN").Single() == Token);
        }

        [Fact, DisplayName("ListarIssues: título cortado em 200 caracteres")]
        public async Task ListarIssues_CortaTitulo()
        {
            var longo = new string('x', 500);
            var (gitlab, _, _) = Criar(_ => Json(HttpStatusCode.OK, $$"""[{"iid":1,"title":"{{longo}}","state":"opened","labels":[]}]"""));

            var r = await gitlab.ListarIssuesAsync("valuto", 5, Ct);

            r.Value.Single().Titulo.Should().HaveLength(200);
        }

        [Fact, DisplayName("ListarMergeRequests: alvo main, estado todos, iid, origem, descrição e status do pipeline da MR")]
        public async Task ListarMergeRequests_Mapeia()
        {
            var (gitlab, handler, _) = Criar(_ => Json(HttpStatusCode.OK, """
                [{"iid":3,"title":"B2 — Cadastro","state":"opened","source_branch":"feature/b2-cadastro","description":"Closes #7","head_pipeline":{"status":"success"}},
                 {"iid":4,"title":"Hotfix: x","state":"merged","source_branch":"hotfix/x","description":null,"head_pipeline":null}]
                """));

            var r = await gitlab.ListarMergeRequestsAsync("valuto", 5, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().HaveCount(2);
            r.Value[0].Iid.Should().Be(3);
            r.Value[0].Titulo.Should().Be("B2 — Cadastro");
            r.Value[0].Estado.Should().Be("opened");
            r.Value[0].BranchOrigem.Should().Be("feature/b2-cadastro");
            r.Value[0].Descricao.Should().Be("Closes #7");
            r.Value[0].StatusPipeline.Should().Be("success");
            r.Value[1].StatusPipeline.Should().BeNull();
            r.Value[1].Descricao.Should().BeNull();
            var pedido = handler.Pedidos.Single();
            pedido.Method.Should().Be(HttpMethod.Get);
            pedido.RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/5/merge_requests");
            pedido.RequestUri.Query.Should().Contain("target_branch=main").And.Contain("state=all").And.Contain("per_page=100");
        }

        [Fact, DisplayName("ListarMergeRequests: percorre páginas e corta o título em 200 caracteres")]
        public async Task ListarMergeRequests_PaginaECorta()
        {
            var longo = new string('y', 300);
            var (gitlab, _, _) = Criar(req => req.RequestUri!.Query.Contains("page=2")
                ? Json(HttpStatusCode.OK, """[{"iid":2,"title":"B","state":"closed","source_branch":"b"}]""")
                : Json(HttpStatusCode.OK, $$"""[{"iid":1,"title":"{{longo}}","state":"opened","source_branch":"a"}]""", ("X-Next-Page", "2")));

            var r = await gitlab.ListarMergeRequestsAsync("valuto", 5, Ct);

            r.Value.Select(m => m.Iid).Should().Equal(1L, 2L);
            r.Value[0].Titulo.Should().HaveLength(200);
        }

        [Fact, DisplayName("ObterProjeto: devolve id, caminho com namespace e se está dentro do grupo configurado")]
        public async Task ObterProjeto_DentroDoGrupo()
        {
            var (gitlab, handler, _) = Criar(req => req.RequestUri!.AbsolutePath.EndsWith("/5")
                ? Json(HttpStatusCode.OK, """{"id":5,"path_with_namespace":"valuto/sub/ws","name":"WS","default_branch":"main"}""")
                : Json(HttpStatusCode.OK, """{"id":6,"path_with_namespace":"valuto-outro/x","name":"X"}"""));

            var dentro = await gitlab.ObterProjetoAsync("valuto", 5, Ct);
            var fora = await gitlab.ObterProjetoAsync("valuto", 6, Ct);

            dentro.Value!.Id.Should().Be(5);
            dentro.Value.Caminho.Should().Be("valuto/sub/ws");
            dentro.Value.BranchPadrao.Should().Be("main");
            dentro.Value.PertenceAoGrupo.Should().BeTrue();
            fora.Value!.PertenceAoGrupo.Should().BeFalse();
            handler.Pedidos.Should().OnlyContain(p => p.Method == HttpMethod.Get);
            handler.Pedidos[0].RequestUri!.AbsolutePath.Should().Be("/api/v4/projects/5");
        }

        [Fact, DisplayName("ObterProjeto 404: sucesso com null")]
        public async Task ObterProjeto_404()
        {
            var (gitlab, _, _) = Criar(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            var r = await gitlab.ObterProjetoAsync("valuto", 9, Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeNull();
        }

        [Fact, DisplayName("As três operações: sem token = gitlab.bot-nao-configurado sem chamar; 401 idem; 5xx = gitlab.indisponivel sem vazar o token")]
        public async Task Erros_ComunsAsTresOperacoes()
        {
            var (semToken, handler, _) = Criar(_ => Json(HttpStatusCode.OK, "[]"), comToken: false);
            var (rejeitado, _, _) = Criar(_ => Json(HttpStatusCode.Unauthorized, "{}"));
            var (fora, _, log) = Criar(_ => throw new HttpRequestException($"falha com {Token}"));

            var semTokenErros = new[]
            {
                (await semToken.ListarIssuesAsync("valuto", 5, Ct)).Errors,
                (await semToken.ListarMergeRequestsAsync("valuto", 5, Ct)).Errors,
                (await semToken.ObterProjetoAsync("valuto", 5, Ct)).Errors,
            };
            var rejeitadoErros = new[]
            {
                (await rejeitado.ListarIssuesAsync("valuto", 5, Ct)).Errors,
                (await rejeitado.ListarMergeRequestsAsync("valuto", 5, Ct)).Errors,
                (await rejeitado.ObterProjetoAsync("valuto", 5, Ct)).Errors,
            };
            var foraErros = new[]
            {
                (await fora.ListarIssuesAsync("valuto", 5, Ct)).Errors,
                (await fora.ListarMergeRequestsAsync("valuto", 5, Ct)).Errors,
                (await fora.ObterProjetoAsync("valuto", 5, Ct)).Errors,
            };

            handler.Pedidos.Should().BeEmpty();
            semTokenErros.Concat(rejeitadoErros).Should().OnlyContain(e => e.Single().Code == "gitlab.bot-nao-configurado");
            foraErros.Should().OnlyContain(e => e.Single().Code == "gitlab.indisponivel" && !(e.Single().Code + e.Single().Message).Contains(Token));
            log.Collector.GetSnapshot().Select(l => l.Message + l.Exception).Should().NotContain(m => m.Contains(Token));
        }
    }
}
