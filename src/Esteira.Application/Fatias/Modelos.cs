using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Fatias
{
    // Leitura do Painel (P1.4): tipos puros, sem I/O. Os leitores são tolerantes e nunca lançam por formato quebrado.

    // Repositório de código da Fatia (coluna `Repositório` do mapa); `infra`/`template` não entram.
    public enum RepositorioFatia { Api, Frontend }

    public enum EstadoDaFatia { Planejada, ComSpec, EmExecucao, EmRevisao, ProntaParaEntrega }

    // Uma linha da tabela "Mapa de fatias". `Status` guarda o texto como está; `StatusReconhecido` = `feito` ou `—`.
    // `CaminhoSpec` é o caminho no repositório do workspace (`specs/feature/...`); `IssueMae` é o `#n` do mapa.
    public sealed record LinhaDoMapa(
        string Codigo, string Titulo, string Status, bool StatusReconhecido, bool Feito,
        string? CaminhoSpec, long? IssueMae, IReadOnlyList<RepositorioFatia> Repositorios,
        string RepositorioTexto, bool FormatoAnterior);

    // Cabeçalho da spec. `Flag` ausente conta como sim (contrato); `FormatoAnterior` = falta `risco:`, `flag:` ou `base:`.
    public sealed record CabecalhoDaSpec(string? Titulo, string? Risco, bool Flag, string? Base, bool FormatoAnterior);

    // `<código> — <título>` ou `<código>.<N> — <título>` (filha, `NumeroTask` preenchido).
    public sealed record TituloDeFatia(string Codigo, int? NumeroTask, string Titulo);

    public sealed record TaskDoBloco(
        string Codigo, int NumeroTask, long IssueFilha, bool Feita, string? Sha, bool EmAndamento,
        string? Executor, bool ReviewPass);

    // Bloco `<!-- execucao -->` da issue-mãe. `SuiteCompleta`/`Review`/`Mr`: estado do `[x]`/`[ ]`.
    public sealed record BlocoDeExecucao(
        string? Area, string? Modo, string? Inicio, IReadOnlyList<TaskDoBloco> Tasks,
        bool SuiteCompleta, bool Review, bool Mr)
    {
        public int TasksFeitas => Tasks.Count(t => t.Feita);

        // "N de M tasks"; null se o bloco não tem task.
        public string? Progresso => Tasks.Count == 0 ? null : $"{TasksFeitas} de {Tasks.Count} tasks";
    }

    // Branch `feature/<fatia minúscula>-<slug>`.
    public sealed record BranchDeFatia(string Codigo, string Slug);

    // Commit `<tipo>: <o que fez> (<fatia>.<N> · workspace#<filha>)` + trailer `Executed-by`.
    public sealed record CommitDeTask(string Tipo, string Descricao, string Codigo, int NumeroTask, long IssueFilha, string? Executor);

    // MR lida de um repositório de código da Fatia.
    public sealed record MrDaFatia(RepositorioFatia Repositorio, MergeRequestGitLabDto Mr);

    // `Progresso`: "N de M tasks" (Em execução) ou "CI verde, aguardando você" / "CI vermelho" / "CI rodando" /
    // "em correção" (Em revisão); null quando não há o que dizer. `FormatoAnterior`: peça faltando (ex.: mãe em execução sem bloco).
    public sealed record ResultadoDeEstado(EstadoDaFatia Estado, string? Progresso, bool EmCorrecao, bool FormatoAnterior);

    // Uma Fatia do Painel. `IssueMae` = `Iid` da mãe resolvida; `Risco` vem da spec (null se spec ilegível ou ausente).
    public sealed record FatiaLida(
        string Codigo, string Titulo, string Status, bool StatusReconhecido,
        IReadOnlyList<RepositorioFatia> Repositorios, string? Risco, bool SpecDisponivel, string? CaminhoSpec,
        long? IssueMae, ResultadoDeEstado Estado, bool FormatoAnterior);
}
