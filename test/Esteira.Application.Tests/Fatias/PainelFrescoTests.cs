using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.2, critério 8: a leitura fresca ignora o guardado de 60 s e o limite de 10 s, e nunca serve cópia velha.
    [Trait("Fatia", "P2")]
    public class PainelFrescoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly AplicacaoDoPainel App = new(
            "app-1", "valuto", 10, new Dictionary<RepositorioFatia, long> { [RepositorioFatia.Api] = 20 });

        private sealed class CacheFalso : IPainelCache
        {
            public Dictionary<string, string> Dados { get; } = new();
            public Task<string?> ObterAsync(string chave, CancellationToken ct = default) => Task.FromResult(Dados.GetValueOrDefault(chave));
            public Task GuardarAsync(string chave, string valor, TimeSpan validade, CancellationToken ct = default) { Dados[chave] = valor; return Task.CompletedTask; }
            public Task<bool> TentarGuardarSeAusenteAsync(string chave, string valor, TimeSpan validade, CancellationToken ct = default)
            {
                var novo = Dados.TryAdd(chave, valor);
                return Task.FromResult(novo);
            }
            public Task RemoverAsync(string chave, CancellationToken ct = default) { Dados.Remove(chave); return Task.CompletedTask; }
            public Task RemoverSeValorAsync(string chave, string valor, CancellationToken ct = default) => Task.CompletedTask;
        }

        private static IGitLab GitLab()
        {
            var gitlab = Substitute.For<IGitLab>();
            gitlab.ObterArquivoAsync("valuto", 10, "specs/README.md", "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(FixturesFatias.Ler("mapa-de-fatias.md")));
            gitlab.ObterArquivoAsync("valuto", 10, Arg.Is<string>(c => c != "specs/README.md"), "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Sucesso(null));
            gitlab.ListarIssuesAsync("valuto", 10, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso([]));
            gitlab.ListarMergeRequestsAsync("valuto", Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([]));
            return gitlab;
        }

        private static int LeiturasDoMapa(IGitLab gitlab) =>
            gitlab.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IGitLab.ObterArquivoAsync) && (string)c.GetArguments()[2]! == "specs/README.md");

        [Fact, DisplayName("8: com o guardado ainda válido, ObterFresco lê o GitLab de novo, sem o limite de 10 s do Atualizar")]
        public async Task Le_de_novo_mesmo_com_guardado_valido()
        {
            var gitlab = GitLab();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(), TimeProvider.System, new PainelOptions());
            await servico.ObterAsync(App, Ct);
            (await servico.AtualizarAsync(App, Ct)).IsSuccess.Should().BeTrue();

            var primeira = await servico.ObterFrescoAsync(App, Ct);
            var segunda = await servico.ObterFrescoAsync(App, Ct);

            primeira.Situacao.Should().Be(SituacaoDoPainel.Ok);
            segunda.Situacao.Should().Be(SituacaoDoPainel.Ok);
            LeiturasDoMapa(gitlab).Should().Be(3);
        }

        [Theory, DisplayName("8: GitLab fora ou sem token devolve o erro mesmo havendo cópia velha")]
        [InlineData("gitlab.indisponivel")]
        [InlineData("gitlab.bot-nao-configurado")]
        public async Task Falha_nao_serve_copia(string codigo)
        {
            var gitlab = GitLab();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(), TimeProvider.System, new PainelOptions());
            await servico.ObterAsync(App, Ct);
            gitlab.ObterArquivoAsync("valuto", 10, "specs/README.md", "main", Arg.Any<CancellationToken>())
                .Returns(Result<string?>.Falha(new Erro(codigo, "x", TipoErro.Failure)));

            var r = await servico.ObterFrescoAsync(App, Ct);

            r.Situacao.Should().Be(SituacaoDoPainel.Falha);
            r.Erro!.Code.Should().Be(codigo);
            r.Desatualizado.Should().BeFalse();
        }

        [Fact, DisplayName("8: a leitura fresca renova o guardado para quem vier depois")]
        public async Task Renova_o_guardado()
        {
            var gitlab = GitLab();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(), TimeProvider.System, new PainelOptions());

            await servico.ObterFrescoAsync(App, Ct);
            await servico.ObterAsync(App, Ct);

            LeiturasDoMapa(gitlab).Should().Be(1);
        }
    }
}
