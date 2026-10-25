using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Esteira.Api.Erros;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
    // Esquema de autenticação do BFF (ADR-0004): o cookie opaco `esteira_session` aponta para a sessão no Redis,
    // de onde saem os claims. Access token vencido não é renovado aqui (sem refresh síncrono): vira 401
    // `auth.access-token-expired` e o front chama POST /auth/refresh.
    public sealed partial class SessaoAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessaoStore sessoes,
        IOptions<AutenticacaoSettings> settings) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Esquema = "Sessao";
        public const string CodeNaoAutenticado = "auth.nao-autenticado";
        public const string CodeAccessTokenExpirado = "auth.access-token-expired";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // O id é gerado pela API (32 bytes em base64url); o que não tem essa forma nem chega ao Redis.
        [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
        private static partial Regex FormatoDoId();

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Cookies.TryGetValue(CookieSessao.Nome, out var id) || !FormatoDoId().IsMatch(id))
                return AuthenticateResult.NoResult();

            var sessao = await sessoes.ObterAsync(id);

            if (sessao is null)
                return AuthenticateResult.NoResult();

            if (sessao.ExpiraEm <= DateTimeOffset.UtcNow)
                return AuthenticateResult.Fail(CodeAccessTokenExpirado);

            var identidade = new ClaimsIdentity(MontarClaims(sessao), Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Scheme.Name));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var resultado = await HandleAuthenticateOnceSafeAsync();
            var expirado = resultado.Failure?.Message == CodeAccessTokenExpirado;

            var erro = expirado
                ? new Erro(CodeAccessTokenExpirado, "Sua sessão precisa ser renovada.", TipoErro.Unauthorized)
                : new Erro(CodeNaoAutenticado, "Entre para continuar.", TipoErro.Unauthorized);

            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(RespostaDeErro.De([erro]), Json);
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            var erro = new Erro("auth.acesso-negado", "Você não tem permissão para isso.", TipoErro.Forbidden);

            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(RespostaDeErro.De([erro]), Json);
        }

        private List<Claim> MontarClaims(SessaoUsuario sessao)
        {
            var acesso = JwtLeitor.LerPayload(sessao.AccessToken);
            var identidade = JwtLeitor.LerPayload(sessao.IdToken);

            // Nome e e-mail podem faltar no access token conforme os escopos; o id_token completa.
            string? Texto(string nome) => JwtLeitor.Texto(acesso, nome) ?? JwtLeitor.Texto(identidade, nome);

            var claims = new List<Claim> { new(UsuarioAtual.ClaimId, sessao.Sub) };

            (string Tipo, string NoToken)[] mapa =
            [
                (settings.Value.ClaimUsuario, settings.Value.ClaimUsuario),
                (settings.Value.ClaimNome, settings.Value.ClaimNome),
                (UsuarioAtual.ClaimEmail, "email")
            ];

            foreach (var (tipo, noToken) in mapa)
            {
                if (Texto(noToken) is { Length: > 0 } valor)
                    claims.Add(new Claim(tipo, valor));
            }

            claims.AddRange(JwtLeitor.Perfis(acesso).Select(perfil => new Claim(UsuarioAtual.ClaimPerfil, perfil)));
            return claims;
        }
    }
}
