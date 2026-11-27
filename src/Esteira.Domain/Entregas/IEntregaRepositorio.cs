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
    }
}
