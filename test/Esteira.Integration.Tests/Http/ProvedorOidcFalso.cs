using System.Text;
using System.Text.Json;
using Esteira.Api.Autenticacao;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Integration.Tests.Http
{
    // Keycloak de mentira para os testes de sessão: nenhuma chamada de rede, respostas programadas pelo teste.
    public sealed class ProvedorOidcFalso : IProvedorOidc
    {
        public static readonly EndpointsOidc Endpoints = new(
            new Uri("https://keycloak.fake/realms/esteira/protocol/openid-connect/auth"),
            new Uri("https://keycloak.fake/realms/esteira/protocol/openid-connect/token"),
            new Uri("https://keycloak.fake/realms/esteira/protocol/openid-connect/logout"));

        private readonly List<(string Code, string Verifier, string RedirectUri)> _trocas = [];

        public IReadOnlyList<(string Code, string Verifier, string RedirectUri)> Trocas
        {
            get { lock (_trocas) return _trocas.ToArray(); }
        }

        // Padrão: o provedor recusa. Cada teste programa o que quer.
        public Func<string, Result<TokensOidc>> ResponderTroca { get; set; } =
            _ => Result<TokensOidc>.Falha(new Erro("auth.login-falhou", "recusado", TipoErro.Unauthorized));

        public Task<EndpointsOidc> ObterEndpointsAsync() => Task.FromResult(Endpoints);

        public Task<Result<TokensOidc>> TrocarCodeAsync(string code, string codeVerifier, string redirectUri)
        {
            lock (_trocas) _trocas.Add((code, codeVerifier, redirectUri));
            return Task.FromResult(ResponderTroca(code));
        }

        // JWT sem assinatura: a API só lê o payload (JwtLeitor).
        public static string Jwt(object payload)
        {
            static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{B64(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"))}.{B64(JsonSerializer.SerializeToUtf8Bytes(payload))}.sem-assinatura";
        }

        public static TokensOidc Tokens(string nonce, string sub = "user-1", string[]? perfis = null, int expiresIn = 300, int refreshExpiresIn = 1800)
            => new(
                Jwt(new { sub, preferred_username = "dev", name = "Dev Teste", email = "dev@esteira.local", realm_access = new { roles = perfis ?? ["mor"] } }),
                "refresh-token-secreto",
                Jwt(new { sub, nonce }),
                expiresIn,
                refreshExpiresIn);
    }
}
