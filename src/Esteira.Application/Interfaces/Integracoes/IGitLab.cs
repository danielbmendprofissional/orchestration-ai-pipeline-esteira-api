using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Integracoes
{
    public sealed record GrupoGitLabDto(long Id, string Caminho, string Nome);

    // BranchPadrao: `default_branch` do projeto (null em repositório vazio ou quando o GitLab não informa).
    public sealed record ProjetoGitLabDto(long Id, string Caminho, string Nome, string? BranchPadrao = null);

    // Porta de leitura do GitLab (A1; escrita só como Ação do bot, ADR-0010). O token do bot é por grupo
    // (`GitLab:Bots:<caminho-do-grupo>:Token`), por isso toda chamada leva o caminho do grupo raiz da Aplicação.
    // Erros: gitlab.bot-nao-configurado, gitlab.grupo-nao-encontrado, gitlab.indisponivel.
    public interface IGitLab
    {
        Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default);

        // Sucesso com null = o arquivo não existe nessa ref.
        Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default);
    }
}
