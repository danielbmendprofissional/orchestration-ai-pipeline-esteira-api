using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Fatias
{
    // Leitura das Ações do bot para o Painel e a gaveta (P2.5), em uma consulta para todas as Aplicações da resposta.
    // O repositório (api | frontend) vem da stack do Projeto da MR; quem pediu, do nome da Pessoa.
    public sealed class AcoesDaFatiaQuery(EsteiraReadContext contexto) : IAcoesDaFatiaQuery
    {
        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<AcaoNaFatia>>> ListarAsync(
            IReadOnlyList<Guid> aplicacoes, CancellationToken cancellationToken = default)
        {
            if (aplicacoes.Count == 0)
                return new Dictionary<Guid, IReadOnlyList<AcaoNaFatia>>();

            var linhas = await (
                from a in contexto.Set<AcaoBotEntity>()
                join app in contexto.Set<AplicacaoEntity>() on a.AplicacaoId equals app.Id
                join p in contexto.Set<ProjetoEntity>() on a.ProjetoId equals p.Id
                join pe in contexto.Set<PessoaEntity>() on a.PedidaPorPessoaId equals pe.Id
                where aplicacoes.Contains(app.PublicId)
                orderby a.PedidaEm, a.Id
                select new { App = app.PublicId, a.PublicId, a.FatiaCodigo, a.Tipo, a.Estado, a.ProjetoId, p.Stack, a.MrIid, pe.Nome, a.PedidaEm, a.AtualizadaEm, a.CodigoErro, a.Comentario })
                .ToListAsync(cancellationToken);

            return linhas.GroupBy(l => l.App).ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AcaoNaFatia>)g.Select(l => new AcaoNaFatia(
                    l.PublicId, l.FatiaCodigo, l.Tipo, l.Estado, l.ProjetoId,
                    l.Stack == Stack.Dotnet ? RepositorioFatia.Api : l.Stack == Stack.Angular ? RepositorioFatia.Frontend : null,
                    l.MrIid, l.Nome, l.PedidaEm, l.AtualizadaEm, l.CodigoErro, l.Comentario)).ToList());
        }
    }
}
