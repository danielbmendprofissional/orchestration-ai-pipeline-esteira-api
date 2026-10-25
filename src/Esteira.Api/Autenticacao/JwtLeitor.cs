using System.Text.Json;

namespace Esteira.Api.Autenticacao
{
    // Lê o payload de um JWT SEM validar assinatura. Só serve para tokens que a API recebeu direto do
    // endpoint de token do Keycloak (canal de fundo, TLS, client confidencial) — nunca para token vindo do navegador.
    public static class JwtLeitor
    {
        public static JsonElement? LerPayload(string? token)
        {
            var partes = token?.Split('.');

            if (partes is not { Length: 3 })
                return null;

            try
            {
                var base64 = partes[1].Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                using var documento = JsonDocument.Parse(Convert.FromBase64String(base64));
                return documento.RootElement.Clone();
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                return null;
            }
        }

        public static string? Texto(JsonElement? payload, string nome)
            => payload is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString()
                : null;

        // Roles do realm: realm_access.roles.
        public static IEnumerable<string> Perfis(JsonElement? payload)
        {
            if (payload is { ValueKind: JsonValueKind.Object } p
                && p.TryGetProperty("realm_access", out var acesso) && acesso.ValueKind == JsonValueKind.Object
                && acesso.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roles.EnumerateArray())
                {
                    if (role.ValueKind == JsonValueKind.String && role.GetString() is { Length: > 0 } texto)
                        yield return texto;
                }
            }
        }
    }
}
