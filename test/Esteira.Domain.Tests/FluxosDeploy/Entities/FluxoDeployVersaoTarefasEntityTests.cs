using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployVersaoTarefasEntityTests
    {
        [Fact, DisplayName("Deve criar a versão das tarefas com os identificadores informados")]
        public void CriarVersaoTarefas_Valido_DeveCriar()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "VERSAO");

            fluxo.CriarVersaoTarefas(2, 3);

            fluxo.VersaoTarefas.IdProjeto.Should().Be(2);
            fluxo.VersaoTarefas.IdVersao.Should().Be(3);
        }

        [Fact, DisplayName("Deve atualizar a versão das tarefas existente ao informar novamente")]
        public void CriarVersaoTarefas_Existente_DeveAtualizar()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "VERSAO");
            fluxo.CriarVersaoTarefas(2, 3);
            var versao = fluxo.VersaoTarefas;

            fluxo.CriarVersaoTarefas(4, 5);

            fluxo.VersaoTarefas.Should().BeSameAs(versao);
            fluxo.VersaoTarefas.IdProjeto.Should().Be(4);
            fluxo.VersaoTarefas.IdVersao.Should().Be(5);
        }

        [Theory, DisplayName("Deve lançar exceção quando algum identificador for inválido")]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-1, 1)]
        public void CriarVersaoTarefas_IdsInvalidos_DeveLancarDomainException(int idProjeto, int idVersao)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "VERSAO");

            var act = () => fluxo.CriarVersaoTarefas(idProjeto, idVersao);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo não estiver em execução")]
        public void CriarVersaoTarefas_FluxoConcluido_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "VERSAO");
            fluxo.Concluir();

            var act = () => fluxo.CriarVersaoTarefas(1, 1);

            act.Should().Throw<DomainException>();
        }
    }
}
