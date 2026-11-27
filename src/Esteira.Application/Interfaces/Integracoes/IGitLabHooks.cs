using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Integracoes
{
    // Hook de projeto do GitLab como a listagem o devolve: só o id e a URL (o segredo nunca volta nem é lido).
    public sealed record HookGitLabDto(long Id, string Url);

    // O que se registra no GitLab: URL de destino e o segredo compartilhado (vai só no corpo da chamada; nunca em log).
    // Eventos fixos: pipeline e merge request; verificação de SSL ligada.
    public sealed record HookParaRegistrar(string Url, string Segredo);

    // Porta dos hooks do GitLab (T1.4), separada da IGitLab: escrita só como Ação do bot, com o token do bot do grupo.
    // Erros: gitlab.bot-nao-configurado, gitlab.sem-permissao (401/403), gitlab.indisponivel (rede, timeout, 5xx e demais).
    public interface IGitLabHooks
    {
        // Todos os hooks do Projeto (paginado por dentro).
        Task<Result<IReadOnlyList<HookGitLabDto>>> ListarAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default);

        // Cria o hook e devolve o id dele.
        Task<Result<long>> CriarAsync(
            string grupoCaminho, long projetoId, HookParaRegistrar hook, CancellationToken cancellationToken = default);

        // Atualiza o hook existente (PUT) com a mesma URL, o segredo e os eventos.
        Task<Result> AtualizarAsync(
            string grupoCaminho, long projetoId, long hookId, HookParaRegistrar hook, CancellationToken cancellationToken = default);
    }
}
