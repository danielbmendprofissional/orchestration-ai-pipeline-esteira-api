using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployGmudEntityTests
    {
        private const string UrlGmud = "https://tarefas.exemplo/issues/1";

        [Fact, DisplayName("Deve registrar a URL da GMUD informada")]
        public void CriarGmud_ComGmud_DeveRegistrarUrl()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");

            fluxo.CriarGmud(" https://tarefas.exemplo/issues/123 ", null, "usuario");

            fluxo.Gmud.Url.Should().Be("https://tarefas.exemplo/issues/123");
            fluxo.Gmud.Usuario.Should().Be("usuario");
        }

        [Theory, DisplayName("Deve deixar a URL nula quando não houver GMUD")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void CriarGmud_SemGmud_UrlDeveSerNula(string urlGmud)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");

            fluxo.CriarGmud(urlGmud, "justificativa", "usuario");

            fluxo.Gmud.Url.Should().BeNull();
            fluxo.Gmud.Justificativa.Should().Be("justificativa");
        }

        [Theory, DisplayName("Deve exigir justificativa quando não houver GMUD")]
        [InlineData(null)]
        [InlineData("")]
        public void CriarGmud_SemGmudSemJustificativa_DeveLancarDomainException(string justificativa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");

            var act = () => fluxo.CriarGmud(null, justificativa, "usuario");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve registrar a execução com a data informada")]
        public void CriarGmud_DeveRegistrarExecucaoInformada()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");
            var agora = new DateTime(2026, 10, 13, 16, 0, 0);

            fluxo.CriarGmud(UrlGmud, null, "usuario", agora);

            fluxo.Gmud.Execucao.Should().Be(agora);
        }

        [Fact, DisplayName("Deve atualizar a GMUD existente ao informar novamente")]
        public void CriarGmud_Existente_DeveAtualizar()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");
            fluxo.CriarGmud(UrlGmud, null, "usuario");
            var gmud = fluxo.Gmud;

            fluxo.CriarGmud(null, "sem gmud", "outro");

            fluxo.Gmud.Should().BeSameAs(gmud);
            fluxo.Gmud.Url.Should().BeNull();
            fluxo.Gmud.Justificativa.Should().Be("sem gmud");
            fluxo.Gmud.Usuario.Should().Be("outro");
        }

        [Theory, DisplayName("Deve lançar exceção quando o usuário for vazio")]
        [InlineData(null)]
        [InlineData("")]
        public void CriarGmud_UsuarioVazio_DeveLancarDomainException(string usuario)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");

            var act = () => fluxo.CriarGmud(UrlGmud, "justificativa", usuario);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo não estiver em execução")]
        public void CriarGmud_FluxoConcluido_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "GMUD");
            fluxo.Concluir();

            var act = () => fluxo.CriarGmud(UrlGmud, null, "usuario");

            act.Should().Throw<DomainException>();
        }
    }
}
