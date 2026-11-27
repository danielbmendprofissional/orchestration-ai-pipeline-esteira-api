using Esteira.Application.Eventos;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Eventos
{
    public sealed class ProjetoDoWebhookQuery(EsteiraReadContext contexto) : IProjetoDoWebhookQuery
    {
        public async Task<ProjetoDoWebhook?> ObterAtivoAsync(string caminho, CancellationToken cancellationToken = default)
            => await (from p in contexto.Set<ProjetoEntity>()
                      join a in contexto.Set<AplicacaoEntity>() on p.AplicacaoId equals a.Id
                      where p.Caminho == caminho && p.Ativo
                      select new ProjetoDoWebhook(p.Id, p.PublicId, a.Id, a.PublicId))
                .FirstOrDefaultAsync(cancellationToken);
    }
}
