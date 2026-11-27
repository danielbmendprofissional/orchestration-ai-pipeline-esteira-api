using Esteira.Application.Entregas.Queries.Obter;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Entregas
{
    // Leitura da Entrega (EsteiraReadContext, sem tracking) com Fluxos, Fatias e Etapas.
    public sealed class EntregaQuery(EsteiraReadContext contexto) : IEntregaQuery
    {
        public async Task<EntregaParaLeitura?> ObterAsync(Guid entregaPublicId, CancellationToken cancellationToken = default)
        {
            var entrega = await contexto.Set<EntregaEntity>()
                .Include(e => e.Fluxos).ThenInclude(f => f.Fatias)
                .Include(e => e.Fluxos).ThenInclude(f => f.Etapas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.PublicId == entregaPublicId, cancellationToken);
            if (entrega is null)
                return null;

            var aplicacao = await contexto.Set<AplicacaoEntity>()
                .Where(a => a.Id == entrega.AplicacaoId)
                .Select(a => new { a.PublicId, a.Nome })
                .FirstAsync(cancellationToken);
            var pessoa = await contexto.Set<PessoaEntity>()
                .Where(p => p.Id == entrega.AbertaPorPessoaId)
                .Select(p => p.Nome)
                .FirstOrDefaultAsync(cancellationToken);

            var ids = entrega.Fluxos.Select(f => f.ProjetoId).ToList();
            var projetos = await contexto.Set<ProjetoEntity>()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new ProjetoDoFluxo(p.Id, p.PublicId, p.Nome, p.Caminho))
                .ToListAsync(cancellationToken);

            return new EntregaParaLeitura(entrega, aplicacao.PublicId, aplicacao.Nome, pessoa ?? string.Empty, projetos);
        }
    }
}
