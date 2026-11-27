using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    public sealed class ProjetoPorCaminhoQuery(EsteiraReadContext contexto) : IProjetoPorCaminhoQuery
    {
        public async Task<long?> ObterIdAtivoAsync(string caminho, CancellationToken cancellationToken = default)
            => await contexto.Set<ProjetoEntity>().Where(p => p.Caminho == caminho && p.Ativo)
                .Select(p => (long?)p.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
