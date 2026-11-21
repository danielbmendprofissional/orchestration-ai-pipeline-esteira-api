namespace Esteira.Application.Aplicacoes
{
    public enum OperacaoAplicacao
    {
        // Papel qualquer ou MOR.
        Ver,
        // Analista ou MOR: Projetos, Ambientes, Destinos, dependências.
        EditarCadastro,
        // Aprovador técnico ou MOR: política de severidade.
        EditarPolitica,
        // Só MOR.
        GerenciarPapeis,
        // Só MOR: nome, sigla, ativa.
        EditarAplicacao
    }
}
