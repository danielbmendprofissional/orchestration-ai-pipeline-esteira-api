using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    public sealed class DestinosDaPipelineQuery(EsteiraReadContext contexto) : IDestinosDaPipelineQuery
    {
        public async Task<IReadOnlyList<DestinoNaResposta>> ListarAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default)
        {
            var destinos = await contexto.Set<DestinoEntity>()
                .Where(d => d.ProjetoId == projetoId && d.Ambiente == ambiente)
                .OrderBy(d => d.Ordem).ThenBy(d => d.Nome)
                .ToListAsync(cancellationToken);
            return destinos.Select(d => new DestinoNaResposta(d.Nome, d.Ordem, new Dictionary<string, string>(d.Configuracao))).ToList();
        }
    }
}
