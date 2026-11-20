namespace Esteira.Application.Interfaces.Sessao
{
    public interface ISessaoStore
    {
        // Cria a sessão e a indexa no conjunto de sessões do usuário. `duracao` é o TTL (refresh_expires_in).
        Task CriarAsync(SessaoUsuario sessao, TimeSpan duracao);

        Task<SessaoUsuario?> ObterAsync(string id);

        // Troca os tokens da sessão mantendo o id; renova o TTL. false = a sessão não existe mais (logout ou
        // revogação no meio do refresh): nada é recriado.
        Task<bool> AtualizarAsync(SessaoUsuario sessao, TimeSpan duracao);

        Task RevogarAsync(string id);

        // Reuse detection: apaga todas as sessões do usuário.
        Task RevogarTodasDoUsuarioAsync(string sub);

        Task GuardarLoginAsync(string state, ContextoLogin contexto, TimeSpan duracao);

        // Uso único: devolve o contexto e o apaga; null se o state é desconhecido, expirou ou já foi usado.
        Task<ContextoLogin?> ConsumirLoginAsync(string state);

        // Trava curta por sessão para o refresh: só um chamador por vez troca o refresh token. Devolve o token do dono
        // (null = outra chamada está com a trava); só o dono libera.
        Task<string?> TentarTravarRefreshAsync(string id);

        Task LiberarRefreshAsync(string id, string dono);
    }
}
