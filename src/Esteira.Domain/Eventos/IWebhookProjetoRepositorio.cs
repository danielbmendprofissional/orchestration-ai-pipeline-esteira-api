namespace Esteira.Domain.Eventos
{
    // Escrita do registro do hook por Projeto (EsteiraContext). Índice único por ProjetoId no banco.
    public interface IWebhookProjetoRepositorio
    {
        Task<WebhookProjetoEntity?> ObterPorProjetoAsync(long projetoId, CancellationToken cancellationToken = default);

        // Ids (long) dos Projetos ativos que ainda não têm WebhookProjeto: base do serviço de partida.
        Task<IReadOnlyList<long>> ListarProjetosAtivosSemWebhookAsync(CancellationToken cancellationToken = default);

        Task AdicionarAsync(WebhookProjetoEntity webhook, CancellationToken cancellationToken = default);
    }
}
