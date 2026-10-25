namespace Esteira.Application.Interfaces.Sessao
{
    // Estado de um login em andamento (Authorization Code + PKCE), de uso único, indexado pelo `state`.
    public sealed record ContextoLogin(string CodeVerifier, string Nonce, string ReturnUrl);
}
