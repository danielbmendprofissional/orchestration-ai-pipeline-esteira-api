namespace Esteira.Api.Autenticacao
{
    // Authority e audience vêm do contrato da infra (Keycloak:Authority / Keycloak:ClientId).
    public class AutenticacaoSettings
    {
        public const string Secao = "Autenticacao";

        // Base pública (mesma origem do front e da API): redirect_uri do callback e retorno do logout.
        public string UrlPublica { get; set; } = string.Empty;
        public bool RequerHttps { get; set; } = true;
        public string ClaimUsuario { get; set; } = "preferred_username";
        public string ClaimNome { get; set; } = "name";
    }
}
