using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
using System.ComponentModel;

namespace Esteira.Domain.Tests.FluxosDeploy.Services.Etapas
{
    public class EtapaSimplesServicesTests
    {
        public static IEnumerable<object[]> Servicos => new List<object[]>
        {
            new object[] { new EtapaBancoDeDadosService(), "BD" },
            new object[] { new EtapaEquipeTecnicaService(), "AET" },
            new object[] { new EtapaGmudService(), "GMUD" },
            new object[] { new EtapaVersaoTarefasService(), "VERSAO" }
        };

        [Theory, DisplayName("Deve validar a conclusão quando a etapa do serviço estiver em execução")]
        [MemberData(nameof(Servicos))]
        public void ValidarConclusaoEtapa_EtapaExecutando_DeveRetornarTrue(IEtapaRulesService servico, string sigla)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, sigla);

            servico.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo)).Should().BeTrue();
        }

        [Theory, DisplayName("Deve lançar exceção quando a etapa do serviço não estiver em execução")]
        [MemberData(nameof(Servicos))]
        public void ValidarConclusaoEtapa_EtapaNaoExecutando_DeveLancarExcecao(IEtapaRulesService servico, string sigla)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV", sigla);

            var act = () => servico.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo));

            act.Should().Throw<DomainException>().WithMessage("A etapa não está em EXECUÇÃO para o fluxo de deploy 5");
        }

        [Theory, DisplayName("Deve lançar exceção quando o fluxo estiver concluído")]
        [MemberData(nameof(Servicos))]
        public void ValidarConclusaoEtapa_FluxoConcluido_DeveLancarExcecao(IEtapaRulesService servico, string sigla)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, sigla);
            fluxo.Concluir();

            var act = () => servico.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxo));

            act.Should().Throw<DomainException>().WithMessage("Fluxo de deploy 5 não está EXECUTANDO!");
        }
    }
}
