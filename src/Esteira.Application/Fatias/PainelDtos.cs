namespace Esteira.Application.Fatias
{
    // Linha do Painel. `Estado`: planejada | com-spec | em-execucao | em-revisao | pronta-para-entrega.
    // `Repositorios`: api | frontend. Todo texto vem do GitLab como dado; nunca é HTML.
    public sealed record FatiaDoPainelDto(
        Guid AplicacaoId, string Aplicacao, string Sigla, string Codigo, string Titulo, string Estado, string? Progresso,
        bool EmCorrecao, bool FormatoAnterior, string? Risco, string Status, bool StatusReconhecido,
        IReadOnlyList<string> Repositorios, bool SpecDisponivel,
        Decisoes.AcoesDaFatiaDto? Acoes = null, Decisoes.ReviewDaFatiaDto? Review = null, string? Decisao = null);

    // `Codigo`: painel.sem-workspace | gitlab.bot-nao-configurado | gitlab.indisponivel | painel.indisponivel.
    public sealed record AvisoDoPainelDto(Guid AplicacaoId, string Aplicacao, string Codigo, string Mensagem);

    // Estado da leitura de cada Aplicação consultada. `GeradoEm` nulo = nada foi lido.
    public sealed record AplicacaoDoPainelDto(Guid Id, string Nome, string Sigla, DateTimeOffset? GeradoEm, bool Desatualizado);

    // Resposta de GET /fatias. A página sai nos headers X-*; `Itens` é só a página pedida.
    public sealed record PainelListaDto(
        IReadOnlyList<FatiaDoPainelDto> Itens, int Pagina, int TamanhoPagina, int Total,
        IReadOnlyList<AvisoDoPainelDto> Avisos, IReadOnlyList<AplicacaoDoPainelDto> Aplicacoes,
        DateTimeOffset? GeradoEm, bool Desatualizado)
    {
        public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
    }

    // Gaveta. Todo `Link*` é montado de ids e caminhos conhecidos; null quando não há de onde montar.
    public sealed record GavetaSpecDto(bool Disponivel, string? Caminho, string? Link, string? Risco, bool FormatoAnterior);

    public sealed record GavetaIssueDto(long Numero, string? Titulo, string? Estado, string? Link);

    public sealed record GavetaMrDto(long Iid, string Titulo, string Estado, string Branch, string? StatusPipeline, string? Link, string? LinkBranch);

    public sealed record GavetaRepositorioDto(string Repositorio, string? Branch, string? LinkBranch, IReadOnlyList<GavetaMrDto> Mrs);

    // `LinkCommit`: o commit da task; sem `sha`, o link da branch do repositório da task (ou do único com branch).
    public sealed record GavetaTaskDto(
        string Codigo, int Numero, long Issue, string? LinkIssue, bool Feita, bool EmAndamento, bool ReviewPass,
        string? Sha, string? Executor, string? Repositorio, string? LinkCommit);

    public sealed record GavetaDto(
        Guid AplicacaoId, string Codigo, string Titulo, string Estado, string? Progresso, bool EmCorrecao, bool FormatoAnterior,
        GavetaSpecDto Spec, GavetaIssueDto? Mae, IReadOnlyList<GavetaIssueDto> Filhas,
        IReadOnlyList<GavetaRepositorioDto> Repositorios, IReadOnlyList<GavetaTaskDto> Tasks,
        Decisoes.AcoesDaFatiaDto? Acoes = null, Decisoes.ReviewDaFatiaDto? Review = null, string? Decisao = null,
        IReadOnlyList<Decisoes.AcaoDaFatiaDto>? AcoesPedidas = null);
}
