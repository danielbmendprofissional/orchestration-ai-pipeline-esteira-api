using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Eventos;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Eventos
{
    public sealed class WebhookProjetoRepositorio(EsteiraContext contexto) : IWebhookProjetoRepositorio
    {
        public Task<WebhookProjetoEntity?> ObterPorProjetoAsync(long projetoId, CancellationToken cancellationToken = default)
            => contexto.Set<WebhookProjetoEntity>().FirstOrDefaultAsync(w => w.ProjetoId == projetoId, cancellationToken);

        public async Task<IReadOnlyList<long>> ListarProjetosAtivosSemWebhookAsync(CancellationToken cancellationToken = default)
            => await contexto.Set<ProjetoEntity>()
                .Where(p => p.Ativo && !contexto.Set<WebhookProjetoEntity>().Any(w => w.ProjetoId == p.Id))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

        public async Task AdicionarAsync(WebhookProjetoEntity webhook, CancellationToken cancellationToken = default)
            => await contexto.Set<WebhookProjetoEntity>().AddAsync(webhook, cancellationToken);
    }
}
