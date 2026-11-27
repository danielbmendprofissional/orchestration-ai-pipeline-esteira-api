using System.ComponentModel;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Eventos;

namespace Esteira.Application.Tests.Eventos
{
    // E1b.2, critério 15 da spec: `entrega.atualizada` só leva identificadores e estado.
    [Trait("Fatia", "E1b")]
    public class EntregaAtualizadaCodecTests
    {
        [Fact, DisplayName("15: serializa tipo + aplicacaoId, entregaId, fluxoId, etapa e estado, e le de volta")]
        public void IdaEVolta()
        {
            var evento = new EntregaAtualizada(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "qa", "executando");

            var json = CodecDeEventos.Serializar(evento);

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.EnumerateObject().Select(p => p.Name)
                .Should().BeEquivalentTo(["tipo", "aplicacaoId", "entregaId", "fluxoId", "etapa", "estado"]);
            doc.RootElement.GetProperty("tipo").GetString().Should().Be("entrega.atualizada");
            CodecDeEventos.TentarLer(json, out var lido).Should().BeTrue();
            lido.Should().Be(evento);
        }

        [Theory, DisplayName("15: evento incompleto e descartado")]
        [InlineData("{\"tipo\":\"entrega.atualizada\",\"aplicacaoId\":\"0190a000-0000-7000-8000-000000000001\",\"entregaId\":\"0190a000-0000-7000-8000-000000000002\",\"fluxoId\":\"0190a000-0000-7000-8000-000000000003\",\"etapa\":\"qa\"}")]
        [InlineData("{\"tipo\":\"entrega.atualizada\",\"aplicacaoId\":\"0190a000-0000-7000-8000-000000000001\",\"entregaId\":\"00000000-0000-0000-0000-000000000000\",\"fluxoId\":\"0190a000-0000-7000-8000-000000000003\",\"etapa\":\"qa\",\"estado\":\"executando\"}")]
        public void Incompleto_Descartado(string json)
            => CodecDeEventos.TentarLer(json, out _).Should().BeFalse();
    }
}
