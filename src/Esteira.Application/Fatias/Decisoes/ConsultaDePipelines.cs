using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Fatias;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Fatias.Decisoes
{
    // Consulta periódica da pipeline do `abrir-review` (spec P2b, "Consulta da pipeline"; critério 6): a cada volta lê as
    // Ações `abrir-review` em `executando` com `PipelineId`, consulta o status e conclui: success confirma; failed/canceled
    // falha em preview.pipeline-falhou; passado Review:PrazoMinutos com a pipeline em andamento, falha em
    // preview.prazo-esgotado e cancela a pipeline. Erro transitório do GitLab só loga e fica para a próxima volta.
    public sealed class ConsultaDePipelines(
        IAcaoBotRepositorio acoes,
        ContextoGitLabDaAcao contexto,
        IGitLab gitlab,
        ConclusaoDaAcaoBot conclusao,
        IOpcoesDeRevisao opcoes,
        TimeProvider relogio,
        ILogger<ConsultaDePipelines> log)
    {
        private const string CodigoPipelineFalhou = "preview.pipeline-falhou";
        private const string CodigoPrazoEsgotado = "preview.prazo-esgotado";
        private const string StatusVerde = "success";

        // Uma volta: não derruba o serviço quando uma Ação falha.
        public async Task ExecutarAsync(CancellationToken ct = default)
        {
            var emExecucao = await acoes.ListarEmExecucaoPorTipoAsync(TipoAcaoBot.AbrirReview, ct);
            foreach (var acao in emExecucao)
            {
                if (acao.PipelineId is not { } pipelineId)
                    continue;

                try
                {
                    await ConsultarAsync(acao, pipelineId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Uma Ação problemática não derruba o serviço; as outras seguem.
                    log.LogWarning("Consulta da pipeline da Ação {Acao} falhou ({Tipo}).", acao.PublicId, ex.GetType().Name);
                }
            }
        }

        private async Task ConsultarAsync(AcaoBotEntity acao, long pipelineId, CancellationToken ct)
        {
            var doGitLab = await contexto.ResolverAsync(acao, ct);
            if (doGitLab is null)
                return;

            var pipeline = await gitlab.ObterPipelineAsync(doGitLab.Grupo, doGitLab.ProjetoGitLabId, pipelineId, ct);
            if (!pipeline.IsSuccess)
            {
                // Transitório do GitLab: só loga e tenta de novo na próxima volta.
                log.LogWarning("Ação {Acao}: pipeline {Pipeline} não lida ({Codigo}).",
                    acao.PublicId, pipelineId, pipeline.Errors[0].Code);
                return;
            }

            var status = pipeline.Value.Status;
            if (string.Equals(status, StatusVerde, StringComparison.OrdinalIgnoreCase))
            {
                await conclusao.ConfirmarAsync(acao, ct);
                return;
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                await conclusao.FalharAsync(acao, CodigoPipelineFalhou, ct);
                return;
            }

            // Ainda em andamento: passado o prazo, falha e cancela a pipeline (best-effort).
            var prazo = TimeSpan.FromMinutes(opcoes.PrazoMinutos);
            if (relogio.GetUtcNow().UtcDateTime - acao.AtualizadaEm > prazo)
            {
                await conclusao.FalharAsync(acao, CodigoPrazoEsgotado, ct);
                await CancelarPipelineAsync(doGitLab, pipelineId, acao);
            }
        }

        private async Task CancelarPipelineAsync(ContextoGitLab doGitLab, long pipelineId, AcaoBotEntity acao)
        {
            try
            {
                var cancelada = await gitlab.CancelarPipelineAsync(
                    doGitLab.Grupo, doGitLab.ProjetoGitLabId, pipelineId, CancellationToken.None);
                if (!cancelada.IsSuccess)
                    log.LogWarning("Ação {Acao}: pipeline não cancelada ({Codigo}).", acao.PublicId, cancelada.Errors[0].Code);
            }
            catch (Exception ex)
            {
                log.LogWarning("Ação {Acao}: falha ao cancelar a pipeline ({Tipo}).", acao.PublicId, ex.GetType().Name);
            }
        }
    }
}
