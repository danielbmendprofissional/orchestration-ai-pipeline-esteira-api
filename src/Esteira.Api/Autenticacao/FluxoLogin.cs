using System.Security.Cryptography;
using System.Text;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Application.Sessao;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
    public sealed record LoginIniciado(Uri Url, string State);

    public sealed record LoginConcluido(string SessaoId, string ReturnUrl);

    // Authorization Code + PKCE (S256) contra o Keycloak, ADR-0004. Nenhum token sai daqui para o navegador:
    // o callback só devolve o id da sessão opaca.
    public sealed class FluxoLogin(
        ISessaoStore sessoes,
        IProvedorOidc provedor,
        IOptions<AutenticacaoSettings> settings,
        IConfiguration configuracao,
        ILogger<FluxoLogin> log)
    {
        private static readonly TimeSpan DuracaoDoLogin = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DuracaoPadraoDaSessao = TimeSpan.FromMinutes(30);

        // Erros padronizados do OAuth2 (RFC 6749 §4.1.2.1). O `error` da query vem do navegador: só estes vão pro log.
        private static readonly HashSet<string> ErrosConhecidos = new(StringComparer.Ordinal)
        {
            "access_denied", "invalid_request", "invalid_scope", "login_required", "interaction_required",
            "server_error", "temporarily_unavailable", "unauthorized_client", "unsupported_response_type"
        };

        public string RedirectUri => $"{settings.Value.UrlPublica.TrimEnd('/')}/api/v1/auth/callback";

        public async Task<LoginIniciado> IniciarAsync(string? returnUrl)
        {
            var state = Aleatorio();
            var nonce = Aleatorio();
            var verifier = Aleatorio();
            var desafio = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

            await sessoes.GuardarLoginAsync(state, new ContextoLogin(verifier, nonce, ReturnUrlSeguro.Normalizar(returnUrl)), DuracaoDoLogin);

            var endpoints = await provedor.ObterEndpointsAsync();
            var url = QueryHelpers.AddQueryString(endpoints.Autorizacao.ToString(), new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = configuracao["Keycloak:ClientId"],
                ["redirect_uri"] = RedirectUri,
                ["scope"] = "openid profile email",
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = desafio,
                ["code_challenge_method"] = "S256"
            });

            return new LoginIniciado(new Uri(url), state);
        }

        // `stateDoNavegador` é o cookie de login que /auth/login pôs neste navegador: sem ele um atacante
        // entregaria à vítima o callback da própria conta dele (login CSRF).
        // null = login recusado: o motivo vai pro log, nunca pra URL nem pra resposta.
        public async Task<LoginConcluido?> ConcluirAsync(string? code, string? state, string? erro, string? stateDoNavegador)
        {
            if (!IdentificadorSeguro.Valido(state))
            {
                log.LogWarning("Callback de login sem state válido");
                return null;
            }

            // Consome o contexto primeiro: o state vale uma vez só, mesmo quando o login vai ser recusado.
            var contexto = await sessoes.ConsumirLoginAsync(state!);

            if (contexto is null)
            {
                log.LogWarning("Callback de login com state desconhecido, expirado ou já usado");
                return null;
            }

            if (!MesmoState(state!, stateDoNavegador))
            {
                log.LogWarning("Callback de login sem o cookie de login deste navegador");
                return null;
            }

            if (!string.IsNullOrEmpty(erro))
            {
                log.LogWarning("Keycloak devolveu erro no login: {Erro}", ErrosConhecidos.Contains(erro) ? erro : "desconhecido");
                return null;
            }

            if (string.IsNullOrEmpty(code))
            {
                log.LogWarning("Callback de login sem code");
                return null;
            }

            var troca = await provedor.TrocarCodeAsync(code, contexto.CodeVerifier, RedirectUri);

            if (!troca.IsSuccess)
            {
                log.LogWarning("Troca do code recusada: {Codigo}", troca.Errors[0].Code);
                return null;
            }

            var tokens = troca.Value;

            if (JwtLeitor.Texto(JwtLeitor.LerPayload(tokens.IdToken), "nonce") != contexto.Nonce)
            {
                log.LogWarning("Nonce do id_token diferente do emitido no login");
                return null;
            }

            var sub = JwtLeitor.Texto(JwtLeitor.LerPayload(tokens.AccessToken), "sub");

            if (string.IsNullOrEmpty(sub))
            {
                log.LogWarning("Access token sem sub");
                return null;
            }

            var duracao = tokens.RefreshExpiresIn > 0 ? TimeSpan.FromSeconds(tokens.RefreshExpiresIn) : DuracaoPadraoDaSessao;
            var sessao = new SessaoUsuario(
                Aleatorio(),
                sub,
                tokens.AccessToken,
                tokens.RefreshToken,
                tokens.IdToken,
                DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn));

            await sessoes.CriarAsync(sessao, duracao);

            return new LoginConcluido(sessao.Id, contexto.ReturnUrl);
        }

        private static bool MesmoState(string state, string? doNavegador)
            => doNavegador is not null
               && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(state), Encoding.ASCII.GetBytes(doNavegador));

        private static string Aleatorio() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }
}
