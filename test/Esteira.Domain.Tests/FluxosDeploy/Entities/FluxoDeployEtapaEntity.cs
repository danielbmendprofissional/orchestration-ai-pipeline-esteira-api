using Esteira.Domain.Common.Enums;
using Esteira.Domain.FluxosDeploy.Entities;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployEtapaEntityTests
    {
        private static FluxoDeployEtapaEntity CriarEtapa()
            => FluxoTestesSuporte.CriarFluxo(1, "DEV").Etapas.Single();

        [Fact, DisplayName("Deve iniciar, concluir e reiniciar a etapa")]
        public void Transicoes_DeveAlterarStatus()
        {
            var etapa = CriarEtapa();

            etapa.Concluir();
            etapa.Status.Should().Be(StatusEnum.CONCLUIDO);

            etapa.Reiniciar();
            etapa.Status.Should().Be(StatusEnum.CONCLUIDO);

            etapa.Iniciar();
            etapa.Reiniciar();
            etapa.Status.Should().Be(StatusEnum.NAO_INICIADO);

            etapa.Iniciar();
            etapa.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve cancelar a etapa e limpar a ordem")]
        public void Cancelar_DeveLimparOrdem()
        {
            var etapa = CriarEtapa();

            etapa.Cancelar();

            etapa.Status.Should().Be(StatusEnum.CANCELADO);
            etapa.Ordem.Should().BeNull();
        }

        [Fact, DisplayName("Deve cancelar a etapa mantendo a ordem quando solicitado")]
        public void Cancelar_MantendoOrdem_DeveManterOrdem()
        {
            var etapa = CriarEtapa();

            etapa.Cancelar(limparOrdem: false);

            etapa.Status.Should().Be(StatusEnum.CANCELADO);
            etapa.Ordem.Should().Be(1);
        }

        [Fact, DisplayName("Deve atualizar a ordem da etapa")]
        public void AtualizarOrdem_DeveAtualizar()
        {
            var etapa = CriarEtapa();

            etapa.AtualizarOrdem(5);

            etapa.Ordem.Should().Be(5);
        }
    }
}
