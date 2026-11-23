using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    // Aplicações que o Painel pode mostrar (P1.6): MOR vê todas as ativas; os demais, as ativas em que têm papel.
    // Só Projetos de código ativos entram (stack Dotnet = Api, Angular = Frontend); `Outro` não é repositório de Fatia.
    public sealed class PainelAplicacaoQuery(EsteiraReadContext contexto) : IPainelAplicacaoQuery
    {
        public async Task<IReadOnlyList<AplicacaoNoPainel>> ListarVisiveisAsync(string sub, bool ehMor, CancellationToken cancellationToken = default)
        {
            var papeis = contexto.Set<PapelAplicacaoEntity>();

            var apps = await contexto.Set<AplicacaoEntity>()
                .Where(a => a.Ativa && (ehMor || papeis.Any(p => p.AplicacaoId == a.Id && p.Usuario == sub)))
                .OrderBy(a => a.Nome).ThenBy(a => a.Id)
                .Select(a => new { a.Id, a.PublicId, a.Nome, a.Sigla, a.GrupoGitLabCaminho, a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho })
                .ToListAsync(cancellationToken);

            var ids = apps.Select(a => a.Id).ToList();
            var projetos = (await contexto.Set<ProjetoEntity>()
                    .Where(p => ids.Contains(p.AplicacaoId) && p.Ativo && (p.Stack == Stack.Dotnet || p.Stack == Stack.Angular))
                    .OrderBy(p => p.Id)
                    .Select(p => new { p.Id, p.AplicacaoId, p.ProjetoGitLabId, p.Caminho, p.Stack })
                    .ToListAsync(cancellationToken))
                .ToLookup(p => p.AplicacaoId);

            return apps.Select(a => new AplicacaoNoPainel(
                a.PublicId, a.Nome, a.Sigla, a.GrupoGitLabCaminho, a.WorkspaceGitLabId, a.WorkspaceGitLabCaminho,
                projetos[a.Id].Select(p => new ProjetoNoPainel(
                    p.Stack == Stack.Dotnet ? RepositorioFatia.Api : RepositorioFatia.Frontend, p.ProjetoGitLabId, p.Caminho, p.Id)).ToList()))
                .ToList();
        }
    }
}
