namespace Esteira.Api.Autenticacao
{
    public static class CookieSessao
    {
        public const string Nome = "esteira_session";

        // Cookie curto que amarra um login em andamento ao navegador que o iniciou (guarda o `state`).
        public const string NomeLogin = "esteira_login";
        public const string CaminhoDoLogin = "/api/v1/auth";

        public static CookieOptions Opcoes(bool requerHttps) => new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = requerHttps,
            Path = "/"
        };

        // Lax, não Strict: o callback chega de uma navegação vinda do Keycloak (outro site) e precisa do cookie.
        // Só vai para /api/v1/auth e vive o mesmo que o contexto de login no Redis (5 min).
        public static CookieOptions OpcoesDoLogin(bool requerHttps) => new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = requerHttps,
            Path = CaminhoDoLogin,
            MaxAge = TimeSpan.FromMinutes(5)
        };
    }
}
