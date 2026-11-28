namespace Esteira.Application.Aplicacoes
{
    // Resposta de GET /aplicacoes/{id}/permissoes. PodeEditar = nome/sigla/ativa da Aplicação (MOR).
    public sealed record PermissoesAplicacao(bool PodeEditar, bool PodeEditarCadastro, bool PodeEditarPolitica, bool PodeGerenciarPapeis, bool PodeEditarTetoCusto);

    // Resposta de GET /aplicacoes/permissoes.
    public sealed record PermissoesGlobaisAplicacao(bool PodeCriar);
}
