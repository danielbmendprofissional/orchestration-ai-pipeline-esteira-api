using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.4, critério 11: descartar o instantâneo faz a próxima leitura ir ao GitLab (mostra o merge ou o fechamento).
    [Trait("Fatia", "P2")]
    public class DescarteDoPainelTests
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
                => Task.FromResult(Dados.TryAdd(chave, valor));
            public Task RemoverAsync(string chave, CancellationToken ct = default) { Dados.Remove(chave); return Task.CompletedTask; }
            public Task RemoverSeValorAsync(string chave, string valor, CancellationToken ct = default)
            {
                if (Dados.GetValueOrDefault(chave) == valor)
                    Dados.Remove(chave);
                return Task.CompletedTask;
            }
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

        [Fact, DisplayName("11: depois de descartar, a próxima leitura da Aplicação vai ao GitLab em vez de servir o guardado")]
        public async Task Descartar_forca_nova_leitura()
        {
            var gitlab = GitLab();
            var servico = new PainelDeFatias(gitlab, new CacheFalso(), TimeProvider.System, new PainelOptions());
            await servico.ObterAsync(App, Ct);
            await servico.ObterAsync(App, Ct);
            LeiturasDoMapa(gitlab).Should().Be(1);

            await ((IDescarteDoPainel)servico).DescartarAsync(App.Chave, Ct);
            await servico.ObterAsync(App, Ct);

            LeiturasDoMapa(gitlab).Should().Be(2);
        }

        [Fact, DisplayName("11: descartar não conta no limite de 10 s do Atualizar")]
        public async Task Descartar_nao_consome_o_limite()
        {
            var servico = new PainelDeFatias(GitLab(), new CacheFalso(), TimeProvider.System, new PainelOptions());

            await ((IDescarteDoPainel)servico).DescartarAsync(App.Chave, Ct);

            (await servico.AtualizarAsync(App, Ct)).IsSuccess.Should().BeTrue();
        }
    }
}
