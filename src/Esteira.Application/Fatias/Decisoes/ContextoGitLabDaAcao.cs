using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Fatias.Decisoes
{
    // O que a chamada ao GitLab precisa: caminho do grupo raiz, id do projeto no GitLab e a stack do Projeto
    // (o `abrir-review` só dispara preview no Projeto Angular).
    public sealed record ContextoGitLab(string Grupo, long ProjetoGitLabId, Stack Stack);

    // Resolve o contexto de uma Ação do bot: Aplicação -> grupo do GitLab, Projeto interno -> id no GitLab.
    // Compartilhado pelo executor (aprovar/reprovar/abrir-review) e pela consulta da pipeline.
    public sealed class ContextoGitLabDaAcao(
        IAplicacaoRepositorio aplicacoes, IProjetoRepositorio projetos, ILogger<ContextoGitLabDaAcao> log)
    {
        public async Task<ContextoGitLab?> ResolverAsync(AcaoBotEntity acao, CancellationToken cancellationToken = default)
        {
            var aplicacao = await aplicacoes.ObterAsync(acao.AplicacaoId, cancellationToken);
            if (aplicacao is null)
            {
                log.LogError("Ação {Acao}: Aplicação {Aplicacao} não existe.", acao.PublicId, acao.AplicacaoId);
                return null;
            }

            var doProjeto = await projetos.ProjetosDaAplicacaoAsync(acao.AplicacaoId, cancellationToken);
            var projeto = doProjeto.FirstOrDefault(p => p.Id == acao.ProjetoId);
            if (projeto is null)
            {
                log.LogError("Ação {Acao}: Projeto {Projeto} não existe na Aplicação.", acao.PublicId, acao.ProjetoId);
                return null;
            }

            return new ContextoGitLab(aplicacao.GrupoGitLabCaminho, projeto.ProjetoGitLabId, projeto.Stack);
        }
    }
}
