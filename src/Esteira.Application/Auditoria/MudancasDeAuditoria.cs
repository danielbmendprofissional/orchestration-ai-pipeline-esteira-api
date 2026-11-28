using System.Text.Json;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Auditoria
{
    // Mesmo critério do DestinoEntity (PASSWORD, SENHA, SECRET, TOKEN, KEY, CREDENTIAL).
    public static class CamposSecretos
    {
        private static readonly string[] Indicios = ["PASSWORD", "SENHA", "SECRET", "TOKEN", "KEY", "CREDENTIAL"];

        // Para `~*` do Postgres (a busca por texto ignora esses nomes).
        public static string PadraoRegex => string.Join('|', Indicios);

        public static bool Eh(string caminho) => Indicios.Any(i => caminho.Contains(i, StringComparison.OrdinalIgnoreCase));
    }

    // Antes/depois por campo, montados na leitura a partir do JSON gravado. Objetos aninhados em caminho com ponto;
    // valor ausente = `—`; campo de segredo mascarado (`••••`) quando existe no lado em questão.
    public static class MudancasDeAuditoria
    {
        public const string Ausente = "—";
        public const string Mascara = "••••";

        public static IReadOnlyList<MudancaDto> Montar(AcaoAuditoria acao, string? antes, string? depois)
        {
            var a = Folhas(antes);
            var d = Folhas(depois);

            IEnumerable<string> campos = acao switch
            {
                AcaoAuditoria.Criou => d.Keys,
                AcaoAuditoria.Removeu => a.Keys,
                _ => a.Keys.Union(d.Keys).Where(k => !a.TryGetValue(k, out var va) || !d.TryGetValue(k, out var vd) || va != vd)
            };

            return [.. campos.Select(k => new MudancaDto(k, Valor(k, a), Valor(k, d)))];
        }

        private static string Valor(string campo, Dictionary<string, string> folhas)
            => !folhas.TryGetValue(campo, out var v) ? Ausente : CamposSecretos.Eh(campo) ? Mascara : v;

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
                    folhas[caminho] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText();
            }
        }
    }
}
