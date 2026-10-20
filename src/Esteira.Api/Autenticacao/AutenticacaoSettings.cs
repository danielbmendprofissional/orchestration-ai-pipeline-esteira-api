namespace Esteira.Api.Autenticacao
{
    public class AutenticacaoSettings
    {
        public const string Secao = "Autenticacao";

        public string Authority { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public bool RequerHttps { get; set; } = true;
        public string ClaimUsuario { get; set; } = "preferred_username";
        public string ClaimNome { get; set; } = "name";
    }
}
