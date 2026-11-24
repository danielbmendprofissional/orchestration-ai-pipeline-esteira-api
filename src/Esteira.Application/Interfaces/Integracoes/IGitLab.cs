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

    // Leitura de uma MR por iid para a Ação do bot (P2b): Estado como o GitLab devolve (opened | closed | merged | locked),
    // BranchOrigem = source_branch, ShaCabeca = sha da cabeça, StatusPipeline = `head_pipeline.status` (null se não há).
    public sealed record MergeRequestDaAcaoGitLabDto(
        long Iid, string Estado, string BranchOrigem, string? ShaCabeca, string? StatusPipeline);

    // Trigger token do Projeto criado na hora pelo executor: Id para apagar depois, Token para disparar a pipeline.
    // O Token nunca é logado nem guardado em banco.
    public sealed record TriggerTokenGitLabDto(long Id, string Token);

    // Status de uma pipeline por id (`success`, `failed`, `running`, `pending`...).
    public sealed record PipelineGitLabDto(long Id, string Status);

    // Porta do GitLab (leitura A1; escrita só como Ação do bot, ADR-0010). O token do bot é por grupo
    // (`GitLab:Bots:<caminho-do-grupo>:Token`), por isso toda chamada leva o caminho do grupo raiz da Aplicação.
    // Erros (leitura): gitlab.bot-nao-configurado, gitlab.grupo-nao-encontrado, gitlab.indisponivel.
    // Erros (escrita, alinhados à tabela de `CodigoErro` da spec 03-acoes-do-bot-na-mr.md): gitlab.bot-nao-configurado
    // (token ausente ou BaseAddress), gitlab.sem-permissao (401/403), gitlab.mr-nao-encontrada (404 numa MR),
    // gitlab.merge-recusado (409/406/405 no merge), gitlab.indisponivel (rede, timeout, 5xx, 429 e os 4xx de
    // token/pipeline sem código próprio — ver Suposições da P2b.1). Nenhum token vai para erro nem log.
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

        // ---- Escrita como Ação do bot (P2b, ADR-0010) ----

        // GET na MR por iid: estado, branch de origem, SHA da cabeça e status da pipeline da cabeça.
        // 404 -> gitlab.mr-nao-encontrada; 401/403 -> gitlab.sem-permissao.
        Task<Result<MergeRequestDaAcaoGitLabDto>> ObterMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default);

        // Cria uma nota na MR e devolve o id dela. O corpo já vem formatado (o texto do humano entra citado).
        // 404 -> gitlab.mr-nao-encontrada; 401/403 -> gitlab.sem-permissao.
        Task<Result<long>> ComentarMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, string corpo, CancellationToken cancellationToken = default);

        // Fecha a MR e devolve o estado que o GitLab informou (esperado: closed).
        Task<Result<MergeRequestDaAcaoGitLabDto>> FecharMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default);

        // Reabre a MR (capacidade da porta; nenhuma rota da P2b a pede) e devolve o estado informado (esperado: opened).
        Task<Result<MergeRequestDaAcaoGitLabDto>> ReabrirMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default);

        // Mergeia a MR com merge commit, sem squash e sem apagar a branch de origem, e devolve o estado informado
        // (esperado: merged). 409/406/405 -> gitlab.merge-recusado; 404 -> gitlab.mr-nao-encontrada.
        Task<Result<MergeRequestDaAcaoGitLabDto>> MergearMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default);

        // Cria trigger token do Projeto com a descrição recebida e devolve id e token. O token nunca é logado.
        Task<Result<TriggerTokenGitLabDto>> CriarTriggerTokenAsync(
            string grupoCaminho, long projetoId, string descricao, CancellationToken cancellationToken = default);

        // Apaga o trigger token do Projeto. Sucesso também quando ele já não existe (404): apagar é idempotente.
        Task<Result> ApagarTriggerTokenAsync(
            string grupoCaminho, long projetoId, long triggerTokenId, CancellationToken cancellationToken = default);

        // Dispara a pipeline pelo trigger token na ref informada, com as variáveis recebidas no dicionário, e devolve
        // o id da pipeline criada. O trigger token autentica a chamada e nunca vai para erro nem log.
        Task<Result<long>> CriarPipelinePorTriggerAsync(
            string grupoCaminho, long projetoId, string triggerToken, string referencia,
            IReadOnlyDictionary<string, string> variaveis, CancellationToken cancellationToken = default);

        // GET no status da pipeline por id.
        Task<Result<PipelineGitLabDto>> ObterPipelineAsync(
            string grupoCaminho, long projetoId, long pipelineId, CancellationToken cancellationToken = default);
    }
}
