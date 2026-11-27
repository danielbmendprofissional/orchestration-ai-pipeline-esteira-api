using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    public sealed class ProjetoDaPipelineQuery(EsteiraReadContext contexto) : IProjetoDaPipelineQuery
    {
        public Task<bool> ProjetoAtivoCadastradoAsync(string caminho, CancellationToken cancellationToken = default)
            => contexto.Set<ProjetoEntity>().AnyAsync(p => p.Caminho == caminho && p.Ativo, cancellationToken);
    }
}
