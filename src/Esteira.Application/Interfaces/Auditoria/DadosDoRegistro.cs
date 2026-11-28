using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Auditoria
{
    // X1: o que o registro de auditoria guarda além de quem/quando/antes/depois. `AplicacaoId` é o Id interno
    // (nulo = fora de qualquer Aplicação). `UsuarioNome` nulo = nome de quem agiu (tela) ou o padrão da origem.
    public sealed record DadosDoRegistro(
        long? AplicacaoId,
        CategoriaAuditoria Categoria,
        OrigemAuditoria Origem = OrigemAuditoria.Tela,
        string? UsuarioNome = null,
        string? Verbo = null,
        string? ObjetoCodigo = null,
        string? ObjetoNome = null,
        string? Motivo = null,
        bool Autoaprovacao = false);
}
