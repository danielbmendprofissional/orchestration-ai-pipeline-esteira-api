using System.Text.RegularExpressions;

namespace Esteira.Application.Sessao
{
    // Id de sessão (cookie) e state de login (query) vêm do cliente e entram na chave do Redis. A API só gera
    // base64url de 32 bytes (43 caracteres); o que não tem essa forma nem chega ao Redis.
    public static partial class IdentificadorSeguro
    {
        // \z, não $: no .NET o $ também casa antes de um "\n" final.
        [GeneratedRegex(@"^[A-Za-z0-9_-]{16,64}\z")]
        private static partial Regex Formato();

        public static bool Valido([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? valor) => valor is not null && Formato().IsMatch(valor);
    }
}
