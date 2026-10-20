using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployBdCrsEntityTests
    {
        [Fact, DisplayName("Deve adicionar a CRS ao fluxo normalizando a URL")]
        public void AdicionarBd_DeveNormalizarUrl()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");

            fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/1/");

            var crs = fluxo.Bds.Should().ContainSingle().Subject;
            crs.Crs.Should().Be("CRS-1");
            crs.Url.Should().Be("https://tarefas.exemplo/issues/1");
        }

        [Theory, DisplayName("Deve lançar exceção quando o CRS for vazio")]
        [InlineData(null)]
        [InlineData("")]
        public void AdicionarBd_CrsVazio_DeveLancarDomainException(string crs)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");

            var act = () => fluxo.AdicionarBd(crs, "https://tarefas.exemplo/issues/1");

            act.Should().Throw<DomainException>();
        }

        [Theory, DisplayName("Deve lançar exceção quando a URL for vazia ou inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nao-e-url")]
        public void AdicionarBd_UrlInvalida_DeveLancarDomainException(string url)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");

            var act = () => fluxo.AdicionarBd("CRS-1", url);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao adicionar CRS já existente no fluxo")]
        public void AdicionarBd_Duplicada_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");
            fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/1");

            var act = () => fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/2");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve remover a CRS do fluxo pelo id")]
        public void RemoverBd_DeveRemover()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");
            fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/1");
            fluxo.Bds.Single().Id = 7;

            fluxo.RemoverBd(7);

            fluxo.Bds.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar exceção ao remover CRS inexistente")]
        public void RemoverBd_Inexistente_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "BD");

            var act = () => fluxo.RemoverBd(99);

            act.Should().Throw<DomainException>();
        }
    }
}
