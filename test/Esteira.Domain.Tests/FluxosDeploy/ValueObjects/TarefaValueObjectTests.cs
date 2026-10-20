using Esteira.Domain.FluxosDeploy.ValueObjects;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.FluxosDeploy.ValueObjects
{
    public class TarefaValueObjectTests
    {
        [Fact, DisplayName("Deve considerar aprovada a tarefa com status Aprovado")]
        public void EstaAprovada_StatusAprovado_DeveRetornarVerdadeiro()
        {
            new TarefaValueObject(1, TarefaValueObject.StatusAprovado).EstaAprovada.Should().BeTrue();
        }

        [Theory, DisplayName("Não deve considerar aprovada a tarefa com outro status")]
        [InlineData("Pendente")]
        [InlineData("aprovado")]
        [InlineData("")]
        [InlineData(null)]
        public void EstaAprovada_OutroStatus_DeveRetornarFalso(string status)
        {
            new TarefaValueObject(1, status).EstaAprovada.Should().BeFalse();
        }

        [Fact, DisplayName("Deve manter o valor constante do status aprovado")]
        public void StatusAprovado_DeveSerAprovado()
        {
            TarefaValueObject.StatusAprovado.Should().Be("Aprovado");
        }
    }
}
