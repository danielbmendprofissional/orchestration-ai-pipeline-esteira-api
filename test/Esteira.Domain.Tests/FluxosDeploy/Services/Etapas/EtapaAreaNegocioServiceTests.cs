using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
using System.ComponentModel;

namespace Esteira.Domain.Tests.FluxosDeploy.Services.Etapas
{
    public class EtapaAreaNegocioServiceTests
    {
        [Fact, DisplayName("Deve validar a conclusão quando todas as tarefas estiverem aprovadas")]
        public void ValidarConclusaoEtapa_TarefasAprovadas_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "AAN");
            var tarefas = new[] { new TarefaValueObject(1, "Aprovado"), new TarefaValueObject(2, "Aprovado") };

            new EtapaAreaNegocioService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo, tarefas)).Should().BeTrue();
        }

        [Fact, DisplayName("Deve validar a conclusão quando não houver tarefas")]
        public void ValidarConclusaoEtapa_SemTarefas_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "AAN");

            new EtapaAreaNegocioService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo, new List<TarefaValueObject>())).Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar exceção listando os ids das tarefas não aprovadas")]
        public void ValidarConclusaoEtapa_TarefasNaoAprovadas_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "AAN");
            var tarefas = new[] { new TarefaValueObject(1, "Aprovado"), new TarefaValueObject(2, "Pendente"), new TarefaValueObject(3, "Rejeitado") };

            var act = () => new EtapaAreaNegocioService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo, tarefas));

            act.Should().Throw<DomainException>().WithMessage("As tarefas não estão aprovadas. IDs: 2, 3");
        }

        [Fact, DisplayName("Deve lançar exceção de domínio quando as tarefas forem nulas")]
        public void ValidarConclusaoEtapa_TarefasNulas_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "AAN");

            var act = () => new EtapaAreaNegocioService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo));

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa AAN não estiver em execução")]
        public void ValidarConclusaoEtapa_EtapaNaoExecutando_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV", "AAN");

            var act = () => new EtapaAreaNegocioService().ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo, new List<TarefaValueObject>()));

            act.Should().Throw<DomainException>().WithMessage("A etapa não está em EXECUÇÃO para o fluxo de deploy 5");
        }
    }
}
