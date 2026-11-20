namespace Esteira.Application.Interfaces.Sessao
{
    // O que o BFF guarda por sessão (ADR-0004): os tokens do Keycloak nunca saem da API.
    public sealed record SessaoUsuario(
        string Id,
        string Sub,
        string AccessToken,
        string RefreshToken,
        string IdToken,
        DateTimeOffset ExpiraEm);
}
