namespace Esteira.Domain.Entregas
{
    // Escrita do agregado Entrega (EsteiraContext). O SaveChanges é do IUnitOfWork.
    public interface IEntregaRepositorio
    {
        Task AdicionarAsync(EntregaEntity entrega, CancellationToken cancellationToken = default);

        // Com Fluxos e Fatias (tracking).
        Task<EntregaEntity?> ObterPorPublicIdAsync(Guid entregaPublicId, CancellationToken cancellationToken = default);

        // O Fluxo pelo PublicId (com Fatias, tracking); quem chama confere EntregaId e Projeto.
        Task<FluxoEntity?> ObterFluxoPorPublicIdAsync(Guid fluxoPublicId, CancellationToken cancellationToken = default);

        // O SHA hoje gravado no banco para o Fluxo (sem tracking): usado após conflito de concorrência, para saber o que o vencedor gravou.
        Task<string?> ObterShaGravadoDoFluxoAsync(long fluxoId, CancellationToken cancellationToken = default);

        // Webhook (E1b): a Etapa (tracking) que guardou esta pipeline, só entre os Fluxos do Projeto. Nunca por SHA nem ref.
        Task<EtapaDaPipeline?> ObterEtapaPorPipelineAsync(long projetoId, long pipelineGitLabId, CancellationToken cancellationToken = default);

        // Relê a Etapa do banco (após conflito de concorrência): o estado do vencedor substitui o valor local.
        Task RecarregarAsync(EtapaFluxoEntity etapa, CancellationToken cancellationToken = default);
    }

    // A Etapa achada pela pipeline e os identificadores públicos do Fluxo e da Entrega, para o evento.
    public sealed record EtapaDaPipeline(EtapaFluxoEntity Etapa, Guid FluxoPublicId, Guid EntregaPublicId);
}
