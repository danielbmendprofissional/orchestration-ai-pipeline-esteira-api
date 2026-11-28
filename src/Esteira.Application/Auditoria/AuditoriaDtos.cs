namespace Esteira.Application.Auditoria
{
    // Formato de leitura da trilha (X1.3). Enums saem em kebab, como no banco e na spec.
    public sealed class RegistroAuditoriaListaDto
    {
        public required Guid Id { get; init; }
        public required DateTime Quando { get; init; }
        public required QuemDto Quem { get; init; }
        public required string Categoria { get; init; }
        public required string Verbo { get; init; }
        public required ObjetoDto Objeto { get; init; }
        public AplicacaoDoRegistroDto? Aplicacao { get; init; }
        public required bool Autoaprovacao { get; init; }
        public required string ResumoMudancas { get; init; }
    }

    // Detalhe (X1.4): os campos da lista mais `mudancas` (antes/depois por campo, segredo mascarado) e `motivo`.
    public sealed class RegistroAuditoriaDetalheDto
    {
        public required Guid Id { get; init; }
        public required DateTime Quando { get; init; }
        public required QuemDto Quem { get; init; }
        public required string Categoria { get; init; }
        public required string Verbo { get; init; }
        public required ObjetoDto Objeto { get; init; }
        public AplicacaoDoRegistroDto? Aplicacao { get; init; }
        public required bool Autoaprovacao { get; init; }
        public required IReadOnlyList<MudancaDto> Mudancas { get; init; }
        public string? Motivo { get; init; }
    }

    public sealed record MudancaDto(string Campo, string Antes, string Depois);

    public sealed record QuemDto(string Nome, string Origem);

    public sealed record ObjetoDto(string Codigo, string? Nome, string Entidade, Guid EntidadeId);

    public sealed record AplicacaoDoRegistroDto(Guid Id, string Nome);

    public sealed record CategoriaContagemDto(string Categoria, int Total);

    public sealed record PessoaDoFiltroDto(string Id, string Nome);

    public sealed class FiltrosDeAuditoriaDto
    {
        public required IReadOnlyList<AplicacaoDoRegistroDto> Aplicacoes { get; init; }
        // `id` = sub da pessoa, ou `@bot` / `@pipeline` quando existem registros dessas origens.
        public required IReadOnlyList<PessoaDoFiltroDto> Pessoas { get; init; }
    }
}
