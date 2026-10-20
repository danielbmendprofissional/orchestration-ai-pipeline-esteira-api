using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
namespace Esteira.Domain.Tests.FluxosDeploy.Services.Etapas
{
    public class EtapaAberturaServiceTests
    {
        [Fact, DisplayName("Deve validar a conclusão da etapa de abertura com versão no padrão vN")]
        public void ValidarConclusaoEtapa_VersaoValida_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "ABER", "DEV");

            new EtapaAberturaService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo)).Should().BeTrue();
        }

        [Theory, DisplayName("Deve lançar exceção quando a versão não seguir o padrão vN")]
        [InlineData("1")]
        [InlineData("V1")]
        [InlineData("v01")]
        [InlineData("v1.0")]
        [InlineData("abc")]
        [InlineData("v0")]
        [InlineData("")]
        public void ValidarConclusaoEtapa_VersaoInvalida_DeveLancarExcecao(string versao)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "ABER", "DEV");
            typeof(FluxoDeployEntity).GetProperty(nameof(FluxoDeployEntity.Versao))!.SetValue(fluxo, versao);

            var act = () => new EtapaAberturaService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo));

            act.Should().Throw<DomainException>().WithMessage("A versão tem que ter que seguir o padrão v1, v2, v3 ...");
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa de abertura não estiver em execução")]
        public void ValidarConclusaoEtapa_EtapaNaoExecutando_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV", "ABER");

            var act = () => new EtapaAberturaService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo));

            act.Should().Throw<DomainException>().WithMessage("A etapa não está em EXECUÇÃO para o fluxo de deploy 5");
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo for nulo")]
        public void ValidarConclusaoEtapa_FluxoNulo_DeveLancarExcecao()
        {
            var act = () => new EtapaAberturaService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(null));

            act.Should().Throw<DomainException>();
        }
    }
}
