using System.ComponentModel;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.AcL.GitLab;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.4, critério 5: o adaptador devolve a branch padrão de cada projeto (a ref de leitura do .gitlab-ci.yml).
    [Trait("Fatia", "A1")]
    public class GitLabBranchPadraoTests
    {
        private sealed class HandlerFalso(string corpo) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }

        [Fact, DisplayName("ListarProjetosDoGrupo traz default_branch; ausente ou null vira BranchPadrao null")]
        public async Task BranchPadrao()
        {
            var opcoes = new GitLabOptions();
            opcoes.Bots["valuto"] = new GitLabBotOptions { Token = "t" };
            var http = new HttpClient(new HandlerFalso(
                """[{"id":1,"path_with_namespace":"valuto/a","name":"A","default_branch":"develop"},{"id":2,"path_with_namespace":"valuto/b","name":"B","default_branch":null},{"id":3,"path_with_namespace":"valuto/c","name":"C"}]"""))
            { BaseAddress = new Uri("https://gitlab.exemplo/") };
            var gitlab = new GitLabHttp(http, Options.Create(opcoes), NullLogger<GitLabHttp>.Instance);

            var r = await gitlab.ListarProjetosDoGrupoAsync("valuto", 42, TestContext.Current.CancellationToken);

            r.Value.Select(p => p.BranchPadrao).Should().Equal("develop", null, null);
        }
    }
}
