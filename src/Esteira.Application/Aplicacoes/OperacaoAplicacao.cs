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
        EditarAplicacao,
        // Só MOR (X3): teto de custo dos agentes do CI.
        EditarTetoCusto,
        // Membro, Aprovador técnico ou MOR (P2): pedir o review de uma Fatia Em revisão.
        AbrirReview,
        // Aprovador técnico ou MOR (P2): aprovar ou reprovar uma Fatia Em revisão.
        DecidirFatia,
        // Membro ou MOR (E1): abrir uma Entrega.
        AbrirEntrega
    }
}
