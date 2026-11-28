using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Fatias.Decisoes
{
    // Conclusão compartilhada de uma Ação do bot: confirma (republica dependentes, grava o estado e, no aprovar/reprovar,
    // dispara o preview-destroy) ou falha. Usada pelo executor e pela consulta periódica da pipeline do `abrir-review`.
    public sealed class ConclusaoDaAcaoBot(
        IAcaoBotRepositorio acoes,
        IAplicacaoRepositorio aplicacoes,
        IProjetoRepositorio projetos,
        IGitLab gitlab,
        RegistrarProgressoDaAcao progresso,
        IPublishEndpoint publicador,
        ILogger<ConclusaoDaAcaoBot> log)
    {
        private const string RefPrincipal = "main";

        public async Task<ResultadoDaExecucao> ConfirmarAsync(Guid acaoPublicId, CancellationToken ct = default)
        {
            var acao = await acoes.ObterPorPublicIdAsync(acaoPublicId, ct);
            if (acao is null)
                return ResultadoDaExecucao.NaoEncontrada;

            return await ConfirmarAsync(acao, ct);
        }

        public async Task<ResultadoDaExecucao> ConfirmarAsync(AcaoBotEntity acao, CancellationToken ct = default, OrigemAuditoria origem = OrigemAuditoria.Bot)
        {
            // Republica a mensagem dos dependentes ainda pedidos ANTES de salvar: o outbox do MassTransit grava no
            // mesmo SaveChanges do RegistrarProgressoDaAcao, então a retomada da Ação do front é atômica com a confirmação.
            var daFatia = await acoes.ListarDaFatiaAsync(acao.AplicacaoId, acao.FatiaCodigo, ct);
            foreach (var dependente in daFatia.Where(a => a.DependeDeAcaoId == acao.Id && a.Estado == EstadoAcaoBot.Pedida))
                await publicador.Publish(
                    new AcaoBotPedida(dependente.PublicId, EnumKebab.Texto(dependente.Tipo), dependente.FatiaCodigo, dependente.ChaveIdempotencia),
                    ct);

            var confirmada = await progresso.RegistrarAsync(acao.PublicId, EstadoAcaoBot.Confirmada, null, ct, origem);
            if (!confirmada.IsSuccess)
            {
                log.LogWarning("Ação {Acao}: não foi possível confirmar ({Codigo}).", acao.PublicId, confirmada.Errors[0].Code);
                return ResultadoDaExecucao.Falhou(confirmada.Errors[0].Code);
            }

            // Critério 8: depois de aprovar/reprovar confirmada, destrói o preview da Fatia — sempre best-effort.
            if (acao.Tipo is TipoAcaoBot.Aprovar or TipoAcaoBot.Reprovar)
                await DestruirPreviewsAsync(acao, ct);

            return ResultadoDaExecucao.Concluida;
        }

        public async Task<ResultadoDaExecucao> FalharAsync(Guid acaoPublicId, string codigo, CancellationToken ct = default)
        {
            var acao = await acoes.ObterPorPublicIdAsync(acaoPublicId, ct);
            if (acao is null)
                return ResultadoDaExecucao.NaoEncontrada;

            return await FalharAsync(acao, codigo, ct);
        }

        public async Task<ResultadoDaExecucao> FalharAsync(AcaoBotEntity acao, string codigo, CancellationToken ct = default, OrigemAuditoria origem = OrigemAuditoria.Bot)
        {
            var falha = await progresso.RegistrarAsync(acao.PublicId, EstadoAcaoBot.Falhou, codigo, ct, origem);
            if (!falha.IsSuccess)
                log.LogWarning("Ação {Acao}: não foi possível registrar a falha ({Codigo}).", acao.PublicId, falha.Errors[0].Code);

            return ResultadoDaExecucao.Falhou(codigo);
        }

        // Para cada Projeto Angular da Fatia que teve `abrir-review` (qualquer estado), dispara o `preview-destroy` na
        // `main`. Qualquer falha aqui só vai para o log: nunca falha nem atrasa a Ação (o schedule `limpeza` do template
        // apaga o preview vencido). Não deduplica disparos repetidos — ver Suposições da P2b.3.
        private async Task DestruirPreviewsAsync(AcaoBotEntity acao, CancellationToken ct)
        {
            List<ProjetoEntity> alvos;
            string grupo;
            try
            {
                var aplicacao = await aplicacoes.ObterAsync(acao.AplicacaoId, ct);
                if (aplicacao is null)
                {
                    log.LogWarning("Ação {Acao}: preview-destroy sem Aplicação {Aplicacao}.", acao.PublicId, acao.AplicacaoId);
                    return;
                }

                var daFatia = await acoes.ListarDaFatiaAsync(acao.AplicacaoId, acao.FatiaCodigo, ct);
                var projetosDaAplicacao = await projetos.ProjetosDaAplicacaoAsync(acao.AplicacaoId, ct);
                alvos = daFatia
                    .Where(a => a.Tipo == TipoAcaoBot.AbrirReview)
                    .Select(a => a.ProjetoId).Distinct()
                    .Select(id => projetosDaAplicacao.FirstOrDefault(p => p.Id == id))
                    .Where(p => p is not null && p.Stack == Stack.Angular)
                    .Select(p => p!)
                    .ToList();
                grupo = aplicacao.GrupoGitLabCaminho;
            }
            catch (Exception ex)
            {
                log.LogWarning("Ação {Acao}: preview-destroy não resolvido ({Tipo}).", acao.PublicId, ex.GetType().Name);
                return;
            }

            foreach (var projeto in alvos)
            {
                try
                {
                    await DispararDestruicaoAsync(grupo, projeto.ProjetoGitLabId, acao, ct);
                }
                catch (Exception ex)
                {
                    log.LogWarning("Ação {Acao}: preview-destroy do Projeto {Projeto} falhou ({Tipo}).",
                        acao.PublicId, projeto.ProjetoGitLabId, ex.GetType().Name);
                }
            }
        }

        private async Task DispararDestruicaoAsync(string grupo, long projetoGitLabId, AcaoBotEntity acao, CancellationToken ct)
        {
            var token = await gitlab.CriarTriggerTokenAsync(
                grupo, projetoGitLabId, $"esteira-acao-destroy-{acao.ChaveIdempotencia}", ct);
            if (!token.IsSuccess)
            {
                log.LogWarning("Ação {Acao}: preview-destroy sem trigger token ({Codigo}).", acao.PublicId, token.Errors[0].Code);
                return;
            }

            try
            {
                var variaveis = new Dictionary<string, string>
                {
                    ["PIPELINE_PROCESS"] = "preview-destroy",
                    ["PREVIEW_FATIA"] = acao.FatiaCodigo.ToLowerInvariant(),
                };
                var pipeline = await gitlab.CriarPipelinePorTriggerAsync(
                    grupo, projetoGitLabId, token.Value.Token, RefPrincipal, variaveis, ct);
                if (!pipeline.IsSuccess)
                    log.LogWarning("Ação {Acao}: preview-destroy não disparado ({Codigo}).", acao.PublicId, pipeline.Errors[0].Code);
            }
            finally
            {
                // Apagar em todo caminho, sem o valor do token; falha só vai para o log.
                var apagado = await gitlab.ApagarTriggerTokenAsync(grupo, projetoGitLabId, token.Value.Id, CancellationToken.None);
                if (!apagado.IsSuccess)
                    log.LogWarning("Ação {Acao}: trigger token do preview-destroy não apagado ({Codigo}).",
                        acao.PublicId, apagado.Errors[0].Code);
            }
        }
    }
}
