using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esteira.Application.Eventos
{
    // Formato no canal Redis e no SSE: JSON camelCase com `tipo` + os campos do evento. Leitura defensiva: o que não
    // for um evento conhecido e completo é descartado (o chamador loga só o motivo, nunca o corpo).
    public static class CodecDeEventos
    {
        private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serializar(EventoDeTempoReal evento)
            => JsonSerializer.Serialize(evento, evento.GetType(), Opcoes);

        public static bool TentarLer(string? json, out EventoDeTempoReal? evento)
        {
            evento = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("tipo", out var tipo) || tipo.ValueKind != JsonValueKind.String)
                    return false;

                EventoDeTempoReal? lido = tipo.GetString() switch
                {
                    TiposDeEvento.AcaoAtualizada => doc.RootElement.Deserialize<AcaoAtualizada>(Opcoes),
                    TiposDeEvento.PipelineAtualizada => doc.RootElement.Deserialize<PipelineAtualizada>(Opcoes),
                    TiposDeEvento.FatiasMudaram => doc.RootElement.Deserialize<FatiasMudaram>(Opcoes),
                    TiposDeEvento.EntregaAtualizada => doc.RootElement.Deserialize<EntregaAtualizada>(Opcoes),
                    _ => null
                };

                if (lido is null || lido.AplicacaoId == Guid.Empty || !Completo(lido))
                    return false;

                evento = lido;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool Completo(EventoDeTempoReal e) => e switch
        {
            AcaoAtualizada a => !string.IsNullOrWhiteSpace(a.FatiaCodigo) && a.AcaoId != Guid.Empty && !string.IsNullOrWhiteSpace(a.Estado),
            PipelineAtualizada p => p.ProjetoId != Guid.Empty && !string.IsNullOrWhiteSpace(p.Status),
            FatiasMudaram => true,
            EntregaAtualizada x => x.EntregaId != Guid.Empty && x.FluxoId != Guid.Empty
                && !string.IsNullOrWhiteSpace(x.Etapa) && !string.IsNullOrWhiteSpace(x.Estado),
            _ => false
        };
    }
}
