using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // P1 (spec 01-painel-de-fatias.md, "Dados"): workspace da Aplicação, os dois campos nulos ou os dois preenchidos.
    [Trait("Fatia", "P1")]
    public class WorkspaceDaAplicacaoTests
    {
        private static AplicacaoEntity Nova() => AplicacaoEntity.Criar("Valuto", "VLT", 10, "valuto").Value;

        [Fact, DisplayName("Deve nascer sem workspace")]
        public void Aplicacao_Criar_SemWorkspace()
        {
            var a = Nova();

            a.WorkspaceGitLabId.Should().BeNull();
            a.WorkspaceGitLabCaminho.Should().BeNull();
        }

        [Fact, DisplayName("Deve guardar o id e o caminho do workspace juntos")]
        public void Aplicacao_DefinirWorkspace_GuardaOsDois()
        {
            var a = Nova();

            var r = a.DefinirWorkspace(77, " valuto/workspace ");

            r.IsSuccess.Should().BeTrue();
            (a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho).Should().Be((77L, "valuto/workspace"));
        }

        [Fact, DisplayName("Deve limpar o id e o caminho do workspace juntos")]
        public void Aplicacao_LimparWorkspace_LimpaOsDois()
        {
            var a = Nova();
            a.DefinirWorkspace(77, "valuto/workspace");

            a.LimparWorkspace();

            a.WorkspaceGitLabId.Should().BeNull();
            a.WorkspaceGitLabCaminho.Should().BeNull();
        }

        [Theory, DisplayName("Deve recusar workspace com id ou caminho incoerente e manter o anterior")]
        [InlineData(0L, "valuto/workspace")]
        [InlineData(-1L, "valuto/workspace")]
        [InlineData(77L, "")]
        [InlineData(77L, "   ")]
        public void Aplicacao_DefinirWorkspace_Incoerente(long id, string caminho)
        {
            var a = Nova();
            a.DefinirWorkspace(5, "valuto/antigo");

            var r = a.DefinirWorkspace(id, caminho);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.workspace-invalido");
            (a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho).Should().Be((5L, "valuto/antigo"));
        }
    }
}
