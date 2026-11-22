using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Integracoes
{
    public sealed record GrupoGitLabDto(long Id, string Caminho, string Nome);

    // BranchPadrao: `default_branch` do projeto (null em repositório vazio ou quando o GitLab não informa).
    public sealed record ProjetoGitLabDto(long Id, string Caminho, string Nome, string? BranchPadrao = null);

    // Leitura do Painel (P1). Textos de terceiros chegam cortados: título em 200 caracteres, descrição em 2000.
    // Estado como o GitLab devolve: opened | closed (issue); opened | closed | merged | locked (MR).
    // Descricao: só vem preenchida nas issues com label `em-execucao` (a mãe, com o bloco de execução), cortada em 10000 caracteres.
    public sealed record IssueGitLabDto(long Iid, string Titulo, string Estado, IReadOnlyList<string> Labels, string? Descricao = null);

    // StatusPipeline: status do pipeline da MR (`head_pipeline`: success, failed, running, pending...); null se não há.
    public sealed record MergeRequestGitLabDto(
        long Iid, string Titulo, string Estado, string BranchOrigem, string? Descricao, string? StatusPipeline);

    // Caminho com namespace; PertenceAoGrupo = o caminho começa com `<grupoCaminho>/`.
    public sealed record ProjetoDoGrupoGitLabDto(long Id, string Caminho, string Nome, string? BranchPadrao, bool PertenceAoGrupo);

    // Porta de leitura do GitLab (A1; escrita só como Ação do bot, ADR-0010). O token do bot é por grupo
    // (`GitLab:Bots:<caminho-do-grupo>:Token`), por isso toda chamada leva o caminho do grupo raiz da Aplicação.
    // Erros: gitlab.bot-nao-configurado, gitlab.grupo-nao-encontrado, gitlab.indisponivel.
    public interface IGitLab
    {
        Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default);

        // Sucesso com null = o arquivo não existe nessa ref.
        Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default);

        // Todas as issues do projeto (abertas e fechadas), paginadas por dentro.
        Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default);

        // Todas as MRs com alvo `main` (qualquer estado), paginadas por dentro.
        Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default);

        // Sucesso com null = o projeto não existe (404) ou o bot não o enxerga (403).
        Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default);
    }
}
