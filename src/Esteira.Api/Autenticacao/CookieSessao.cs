namespace Esteira.Api.Autenticacao
{
    public static class CookieSessao
    {
        public const string Nome = "esteira_session";

        public static CookieOptions Opcoes(bool requerHttps) => new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = requerHttps,
            Path = "/"
        };
    }
}
