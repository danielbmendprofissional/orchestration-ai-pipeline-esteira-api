namespace Esteira.Application.Fatias
{
    // Projeto de código ativo da Aplicação: `Caminho` é o caminho completo no GitLab (base dos links).
    public sealed record ProjetoNoPainel(RepositorioFatia Repositorio, long GitLabId, string Caminho);

    // Aplicação como o Painel a enxerga. `Id` é o PublicId (ADR-0014); o `long` interno não sai do banco.
    public sealed record AplicacaoNoPainel(
        Guid Id, string Nome, string Sigla, string GrupoCaminho, long? WorkspaceId, string? WorkspaceCaminho,
        IReadOnlyList<ProjetoNoPainel> Projetos)
    {
        // Um Projeto por repositório (o primeiro cadastrado); `Chave` do leitor = PublicId em texto.
        public AplicacaoDoPainel Entrada => new(
            Id.ToString(), GrupoCaminho, WorkspaceId,
            Projetos.GroupBy(p => p.Repositorio).ToDictionary(g => g.Key, g => g.First().GitLabId));

        public string? CaminhoDe(RepositorioFatia repositorio) => Projetos.FirstOrDefault(p => p.Repositorio == repositorio)?.Caminho;
    }
}
