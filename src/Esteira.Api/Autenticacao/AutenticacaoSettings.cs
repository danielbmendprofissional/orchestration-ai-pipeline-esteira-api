namespace Esteira.Api.Autenticacao
{
    // Authority e audience vêm do contrato da infra (Keycloak:Authority / Keycloak:ClientId).
    public class AutenticacaoSettings
    {
        public const string Secao = "Autenticacao";

        public bool RequerHttps { get; set; } = true;
        public string ClaimUsuario { get; set; } = "preferred_username";
        public string ClaimNome { get; set; } = "name";
    }
}
