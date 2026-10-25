using System.Text.Json;
using Esteira.Domain.Common.Resultados;
using Microsoft.Extensions.Caching.Memory;

namespace Esteira.Api.Autenticacao
{
    // Fala com o Keycloak do realm `esteira` pelo canal de fundo (client confidencial `esteira-api`).
    // Authority, ClientId e ClientSecret são as chaves do contrato da infra (Keycloak:*).
    public sealed class ProvedorOidcKeycloak(HttpClient http, IConfiguration configuracao, IMemoryCache cache, ILogger<ProvedorOidcKeycloak> log) : IProvedorOidc
    {
        private static readonly TimeSpan DuracaoDoDiscovery = TimeSpan.FromHours(1);

        private string Authority => configuracao["Keycloak:Authority"]!.TrimEnd('/');

        public async Task<EndpointsOidc> ObterEndpointsAsync()
        {
            var endpoints = await cache.GetOrCreateAsync("oidc:endpoints", async entrada =>
            {
                entrada.AbsoluteExpirationRelativeToNow = DuracaoDoDiscovery;
                using var resposta = await http.GetAsync($"{Authority}/.well-known/openid-configuration");
                resposta.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
                var raiz = doc.RootElement;

                return new EndpointsOidc(
                    new Uri(raiz.GetProperty("authorization_endpoint").GetString()!),
                    new Uri(raiz.GetProperty("token_endpoint").GetString()!),
                    raiz.TryGetProperty("end_session_endpoint", out var fim) ? new Uri(fim.GetString()!) : null);
            });

            return endpoints!;
        }

        public async Task<Result<TokensOidc>> TrocarCodeAsync(string code, string codeVerifier, string redirectUri)
            => await ChamarTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            });

        public async Task<Result<TokensOidc>> RenovarAsync(string refreshToken)
            => await ChamarTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

        private async Task<Result<TokensOidc>> ChamarTokenAsync(Dictionary<string, string> campos)
        {
            campos["client_id"] = configuracao["Keycloak:ClientId"]!;
            campos["client_secret"] = configuracao["Keycloak:ClientSecret"]!;

            HttpResponseMessage resposta;
            string corpo;

            try
            {
                var endpoints = await ObterEndpointsAsync();
                resposta = await http.PostAsync(endpoints.Token, new FormUrlEncodedContent(campos));
                corpo = await resposta.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                log.LogError(ex, "Keycloak indisponível na chamada de token");
                return Result<TokensOidc>.Falha(new Erro("auth.provedor-indisponivel", "Serviço de login indisponível. Tente de novo.", TipoErro.Failure));
            }

            using (resposta)
            {
                if (!resposta.IsSuccessStatusCode)
                    return RecusaDoToken(resposta, corpo, campos["grant_type"]);
            }

            using var doc = JsonDocument.Parse(corpo);
            var raiz = doc.RootElement;

            return Result<TokensOidc>.Sucesso(new TokensOidc(
                raiz.GetProperty("access_token").GetString()!,
                raiz.GetProperty("refresh_token").GetString()!,
                raiz.TryGetProperty("id_token", out var id) ? id.GetString()! : string.Empty,
                raiz.GetProperty("expires_in").GetInt32(),
                raiz.TryGetProperty("refresh_expires_in", out var re) ? re.GetInt32() : 0));
        }

        // O corpo do erro do Keycloak (error/error_description) não leva token; vai só pro log.
        private Result<TokensOidc> RecusaDoToken(HttpResponseMessage resposta, string corpo, string grant)
        {
            log.LogWarning("Keycloak recusou {Grant} ({Status}): {Corpo}", grant, (int)resposta.StatusCode, corpo);

            if (grant != "refresh_token")
                return Result<TokensOidc>.Falha(new Erro("auth.login-falhou", "Não foi possível concluir o login.", TipoErro.Unauthorized));

            // Só invalid_grant prova que o refresh token foi usado ou revogado (reuse detection). Qualquer outra
            // recusa (5xx, invalid_client por configuração) não é culpa da sessão e não pode derrubá-la.
            return corpo.Contains("invalid_grant", StringComparison.Ordinal)
                ? Result<TokensOidc>.Falha(new Erro("auth.sessao-invalida", "Sua sessão não é mais válida. Entre de novo.", TipoErro.Unauthorized))
                : Result<TokensOidc>.Falha(new Erro("auth.provedor-indisponivel", "Serviço de login indisponível. Tente de novo.", TipoErro.Failure));
        }
    }
}
