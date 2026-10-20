using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployAprovadorNegocioEntityTests
    {
        [Fact, DisplayName("Deve adicionar a tarefa de aprovação normalizando a URL")]
        public void AdicionarAprovadorNegocio_DeveNormalizarUrl()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");

            fluxo.AdicionarAprovadorNegocio(2, "https://tarefas.exemplo/issues/2/");

            var aprovador = fluxo.AprovadoresNegocio.Should().ContainSingle().Subject;
            aprovador.IdTarefa.Should().Be(2);
            aprovador.Url.Should().Be("https://tarefas.exemplo/issues/2");
        }

        [Theory, DisplayName("Deve lançar exceção quando a tarefa for inválida")]
        [InlineData(0)]
        [InlineData(-1)]
        public void AdicionarAprovadorNegocio_TarefaInvalida_DeveLancarDomainException(int idTarefa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");

            var act = () => fluxo.AdicionarAprovadorNegocio(idTarefa, "https://tarefas.exemplo/issues/1");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a URL for inválida")]
        public void AdicionarAprovadorNegocio_UrlInvalida_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");

            var act = () => fluxo.AdicionarAprovadorNegocio(1, "nao-e-url");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao associar a mesma tarefa duas vezes")]
        public void AdicionarAprovadorNegocio_Duplicada_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");
            fluxo.AdicionarAprovadorNegocio(1, "https://tarefas.exemplo/issues/1");

            var act = () => fluxo.AdicionarAprovadorNegocio(1, "https://tarefas.exemplo/issues/1");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve remover a tarefa de aprovação pelo id da tarefa")]
        public void RemoverAprovadorNegocio_DeveRemover()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");
            fluxo.AdicionarAprovadorNegocio(1, "https://tarefas.exemplo/issues/1");

            fluxo.RemoverAprovadorNegocio(1);

            fluxo.AprovadoresNegocio.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar exceção ao remover tarefa não associada")]
        public void RemoverAprovadorNegocio_Inexistente_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN");

            var act = () => fluxo.RemoverAprovadorNegocio(99);

            act.Should().Throw<DomainException>();
        }
    }
}
