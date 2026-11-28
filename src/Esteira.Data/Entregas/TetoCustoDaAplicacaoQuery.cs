using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    public sealed class TetoCustoDaAplicacaoQuery(EsteiraReadContext contexto) : ITetoCustoDaAplicacaoQuery
    {
        public async Task<decimal?> ObterAsync(long aplicacaoId, CancellationToken cancellationToken = default)
            => await contexto.Set<AplicacaoEntity>()
                .Where(a => a.Id == aplicacaoId)
                .Select(a => a.TetoCustoAgentesUsd)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
