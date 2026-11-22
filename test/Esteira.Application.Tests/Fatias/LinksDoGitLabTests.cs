using AwesomeAssertions;
using Esteira.Application.Fatias;

namespace Esteira.Application.Tests.Fatias
{
    // P1.6, critérios 12 e 17: links só de ids e caminhos conhecidos; nada vindo do GitLab vira URL sem validar.
    [Trait("Fatia", "P1")]
    public class LinksDoGitLabTests
    {
        private readonly LinksDoGitLab _links = new("https://gitlab.exemplo.com/");

        [Fact]
        public void Monta_os_links_sobre_a_url_do_gitlab()
        {
            _links.Spec("g/app-workspace", "specs/feature/painel/01-painel-de-fatias.md")
                .Should().Be("https://gitlab.exemplo.com/g/app-workspace/-/blob/main/specs/feature/painel/01-painel-de-fatias.md");
            _links.Issue("g/app-workspace", 37).Should().Be("https://gitlab.exemplo.com/g/app-workspace/-/issues/37");
            _links.Branch("g/app-api", "feature/p1-painel-de-fatias").Should().Be("https://gitlab.exemplo.com/g/app-api/-/tree/feature/p1-painel-de-fatias");
            _links.Mr("g/app-api", 5).Should().Be("https://gitlab.exemplo.com/g/app-api/-/merge_requests/5");
            _links.Commit("g/app-api", "0b4bd79").Should().Be("https://gitlab.exemplo.com/g/app-api/-/commit/0b4bd79");
        }

        [Theory]
        [InlineData("feature/../../x")]
        [InlineData("feature/x?y=1")]
        [InlineData("feature/<script>")]
        [InlineData("javascript:alert(1)")]
        [InlineData("")]
        [InlineData(null)]
        public void Branch_fora_do_formato_nao_vira_link(string? branch)
            => _links.Branch("g/app-api", branch).Should().BeNull();

        [Theory]
        [InlineData("specs/../../etc")]
        [InlineData("//evil.com/x")]
        [InlineData("specs/a b.md#x")]
        [InlineData(null)]
        public void Spec_fora_do_formato_nao_vira_link(string? caminho)
            => _links.Spec("g/app-workspace", caminho).Should().BeNull();

        [Theory]
        [InlineData("zzzz")]
        [InlineData("0b4bd79; rm")]
        [InlineData(null)]
        public void Sha_invalido_nao_vira_link(string? sha)
            => _links.Commit("g/app-api", sha).Should().BeNull();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("não é url")]
        public void Sem_url_do_gitlab_nao_ha_link(string? url)
            => new LinksDoGitLab(url).Issue("g/ws", 1).Should().BeNull();

        [Fact]
        public void Sem_caminho_de_projeto_nao_ha_link()
        {
            _links.Mr(null, 5).Should().BeNull();
            _links.Issue(null, 5).Should().BeNull();
        }
    }
}
