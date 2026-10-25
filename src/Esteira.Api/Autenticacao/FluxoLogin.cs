using System.Security.Cryptography;
using System.Text;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Application.Sessao;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
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

        public string RedirectUri => $"{settings.Value.UrlPublica.TrimEnd('/')}/api/v1/auth/callback";

        public async Task<Uri> IniciarAsync(string? returnUrl)
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

            return new Uri(url);
        }

        // null = login recusado: o motivo vai pro log, nunca pra URL nem pra resposta.
        public async Task<LoginConcluido?> ConcluirAsync(string? code, string? state, string? erro)
        {
            if (string.IsNullOrEmpty(state))
            {
                log.LogWarning("Callback de login sem state");
                return null;
            }

            // Consome o contexto primeiro: o state vale uma vez só, mesmo quando o Keycloak devolveu erro.
            var contexto = await sessoes.ConsumirLoginAsync(state);

            if (contexto is null)
            {
                log.LogWarning("Callback de login com state desconhecido, expirado ou já usado");
                return null;
            }

            if (!string.IsNullOrEmpty(erro))
            {
                log.LogWarning("Keycloak devolveu erro no login: {Erro}", erro);
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

        private static string Aleatorio() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }
}
