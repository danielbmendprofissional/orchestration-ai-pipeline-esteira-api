using Esteira.Application.Fatias;
using Esteira.Domain.Fatias;

namespace Esteira.Application.Interfaces.Fatias
{
    // Ação do bot como o Painel a lê (P2.5). `ProjetoId` é interno (não sai da API); `Repositorio` vem da stack do Projeto.
    public sealed record AcaoNaFatia(
        Guid Id, string FatiaCodigo, TipoAcaoBot Tipo, EstadoAcaoBot Estado, long ProjetoId, RepositorioFatia? Repositorio, long MrIid,
        string? PedidaPor, DateTime PedidaEm, DateTime AtualizadaEm, string? CodigoErro, string? Comentario);

    // Leitura das Ações (EsteiraReadContext), por PublicId da Aplicação, da mais antiga para a mais nova.
    public interface IAcoesDaFatiaQuery
    {
        Task<IReadOnlyDictionary<Guid, IReadOnlyList<AcaoNaFatia>>> ListarAsync(IReadOnlyList<Guid> aplicacoes, CancellationToken cancellationToken = default);
    }

    // Flag `Features:P2` e `Review:UrlModelo`, lidos da configuração a cada uso (nada é fixado na partida).
    public interface IOpcoesDeRevisao
    {
        bool Ligado { get; }

        string? UrlModelo { get; }
    }
}
