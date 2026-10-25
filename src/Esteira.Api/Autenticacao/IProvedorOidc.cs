using Esteira.Domain.Common.Resultados;

namespace Esteira.Api.Autenticacao
{
    public sealed record EndpointsOidc(Uri Autorizacao, Uri Token, Uri? EndSession);

    public sealed record TokensOidc(string AccessToken, string RefreshToken, string IdToken, int ExpiresIn, int RefreshExpiresIn);

    // Porta do Keycloak (OIDC) para o BFF: discovery e troca de code. O adaptador de verdade é o
    // ProvedorOidcKeycloak; os testes de integração a substituem por um provedor falso.
    public interface IProvedorOidc
    {
        Task<EndpointsOidc> ObterEndpointsAsync();

        Task<Result<TokensOidc>> TrocarCodeAsync(string code, string codeVerifier, string redirectUri);
    }
}
