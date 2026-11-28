using System.Text.Json;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Auditoria
{
    // Texto curto dos campos alterados, para a lista. Só nomes de campo (nunca valor), então não vaza segredo.
    // O detalhe antes/depois com mascaramento é da X1.4.
    public static class ResumoDeMudancas
    {
        private const int Limite = 3;

        public static string Montar(AcaoAuditoria acao, string? antes, string? depois)
        {
            var a = Folhas(antes);
            var d = Folhas(depois);

            IEnumerable<string> campos = acao switch
            {
                AcaoAuditoria.Criou => d.Keys,
                AcaoAuditoria.Removeu => a.Keys,
                _ => a.Keys.Union(d.Keys).Where(k => !a.TryGetValue(k, out var va) || !d.TryGetValue(k, out var vd) || va != vd)
            };

            var lista = campos.ToList();
            if (lista.Count <= Limite)
                return string.Join(", ", lista);

            return $"{string.Join(", ", lista.Take(Limite))} e mais {lista.Count - Limite}";
        }

        // Caminho com ponto (`destino.replicas`) -> texto bruto do valor; ordem de aparição preservada.
        private static Dictionary<string, string> Folhas(string? json)
        {
            var folhas = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json))
                return folhas;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    Achatar(doc.RootElement, "", folhas);
            }
            catch (JsonException)
            {
            }

            return folhas;
        }

        private static void Achatar(JsonElement objeto, string prefixo, Dictionary<string, string> folhas)
        {
            foreach (var p in objeto.EnumerateObject())
            {
                var caminho = prefixo.Length == 0 ? p.Name : $"{prefixo}.{p.Name}";
                if (p.Value.ValueKind == JsonValueKind.Object)
                    Achatar(p.Value, caminho, folhas);
                else
                    folhas[caminho] = p.Value.GetRawText();
            }
        }
    }
}
