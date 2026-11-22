using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // P1.5: a issue-mãe com label `em-execucao` traz a descrição (bloco de execução); as demais não.
    [Trait("Fatia", "P1")]
    public class GitLabHttpDescricaoDaMaeTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class HandlerFalso(string corpo) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }

        private static GitLabHttp Criar(string corpo)
        {
            var opcoes = new GitLabOptions();
            opcoes.Bots["valuto"] = new GitLabBotOptions { Token = "t" };
            var http = new HttpClient(new HandlerFalso(corpo)) { BaseAddress = new Uri("https://gitlab.exemplo/") };
            return new GitLabHttp(http, Options.Create(opcoes), new FakeLogger<GitLabHttp>());
        }

        [Fact, DisplayName("ListarIssues: só issue com label em-execucao traz Descricao; as outras vêm sem")]
        public async Task So_issue_em_execucao_traz_descricao()
        {
            var gitlab = Criar("""
                [{"iid":1,"title":"B2 — Mãe","state":"opened","labels":["em-execucao"],"description":"<!-- execucao -->\n- [x] 1"},
                 {"iid":2,"title":"B3 — Outra","state":"opened","labels":[],"description":"texto"},
                 {"iid":3,"title":"B2.1 — Filha","state":"opened","labels":["em-execucao"],"description":"filha"}]
                """);

            var r = await gitlab.ListarIssuesAsync("valuto", 5, Ct);

            r.Value[0].Descricao.Should().Contain("<!-- execucao -->");
            r.Value[1].Descricao.Should().BeNull();
            r.Value[2].Descricao.Should().Be("filha");
        }

        [Fact, DisplayName("ListarIssues: descrição da mãe cortada em 10000 caracteres")]
        public async Task Descricao_cortada()
        {
            var longa = new string('x', 20000);
            var gitlab = Criar($$"""[{"iid":1,"title":"B2 — Mãe","state":"opened","labels":["em-execucao"],"description":"{{longa}}"}]""");

            var r = await gitlab.ListarIssuesAsync("valuto", 5, Ct);

            r.Value.Single().Descricao.Should().HaveLength(10000);
        }
    }
}
