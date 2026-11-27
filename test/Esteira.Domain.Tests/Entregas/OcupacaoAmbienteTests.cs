using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, critérios 11, 12 e 13): reservar e liberar o Ambiente do Projeto.
    [Trait("Fatia", "E1")]
    public class OcupacaoAmbienteTests
    {
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        [Theory, DisplayName("Critério 11: Ambiente livre -> reserva para o Fluxo")]
        [InlineData(AmbienteDeploy.Hml)]
        [InlineData(AmbienteDeploy.Producao)]
        public void ReservaLivre(AmbienteDeploy ambiente)
        {
            var r = OcupacaoAmbienteEntity.Reservar(null, 5, ambiente, 7, T0);

            r.IsSuccess.Should().BeTrue();
            (r.Value!.ProjetoId, r.Value.Ambiente, r.Value.FluxoId, r.Value.OcupadaEm).Should().Be((5L, ambiente, 7L, T0));
        }

        [Fact, DisplayName("Critério 11: o mesmo Fluxo reservando de novo é idempotente (devolve a mesma reserva)")]
        public void MesmoFluxo()
        {
            var atual = OcupacaoAmbienteEntity.Reservar(null, 5, AmbienteDeploy.Hml, 7, T0).Value!;

            var r = OcupacaoAmbienteEntity.Reservar(atual, 5, AmbienteDeploy.Hml, 7, T0.AddMinutes(5));

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeSameAs(atual);
            atual.OcupadaEm.Should().Be(T0);
        }

        [Fact, DisplayName("Critério 12: outro Fluxo -> ambiente.ocupado (Conflict)")]
        public void OutroFluxo()
        {
            var atual = OcupacaoAmbienteEntity.Reservar(null, 5, AmbienteDeploy.Producao, 7, T0).Value!;

            var erro = OcupacaoAmbienteEntity.Reservar(atual, 5, AmbienteDeploy.Producao, 8, T0).Errors.Should().ContainSingle().Subject;

            (erro.Code, erro.Type).Should().Be(("ambiente.ocupado", TipoErro.Conflict));
        }

        [Fact, DisplayName("Critério 13: qa não tem ocupação (nada a reservar)")]
        public void QaSemOcupacao()
        {
            var r = OcupacaoAmbienteEntity.Reservar(null, 5, AmbienteDeploy.Qa, 7, T0);

            r.IsSuccess.Should().BeTrue();
            r.Value.Should().BeNull();
        }

        [Fact, DisplayName("Liberar: só o Fluxo dono libera; outro Fluxo recebe ambiente.ocupado")]
        public void Libera()
        {
            var atual = OcupacaoAmbienteEntity.Reservar(null, 5, AmbienteDeploy.Hml, 7, T0).Value!;

            atual.Liberar(7).IsSuccess.Should().BeTrue();
            atual.Liberar(8).Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.ocupado");
        }
    }
}
