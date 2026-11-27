using System.Text.Json;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Decisoes;
using Esteira.Application.Interfaces.Fatias;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Eventos
{
    // Projeto cadastrado e ativo que o webhook reconheceu pelo caminho. Ids longos para join, GUIDs para o evento.
    public sealed record ProjetoDoWebhook(long ProjetoId, Guid ProjetoPublicId, long AplicacaoId, Guid AplicacaoPublicId);

    // Porta de leitura: o Projeto ativo pelo `project.path_with_namespace` do GitLab; nulo se não cadastrado.
    public interface IProjetoDoWebhookQuery
    {
        Task<ProjetoDoWebhook?> ObterAtivoAsync(string caminho, CancellationToken cancellationToken = default);
    }

    public interface ITratadorDeWebhookGitLab
    {
        // `evento` é o cabeçalho X-Gitlab-Event; `corpo`, o JSON recebido (nunca logado). Sucesso = tratado ou ignorado.
        Task<Result> TratarAsync(string? evento, string corpo, CancellationToken cancellationToken = default);
    }

    // Traduz o Pipeline Hook e o Merge Request Hook (T1.3). O webhook nunca grava agregado novo: conclui a Ação do bot pelo
    // mesmo caminho da consulta periódica (ConclusaoDaAcaoBot), apaga o guardado do Painel e publica no tempo real. O corpo é
    // só lido, nunca logado; o evento sai com identificadores e status, sem texto livre do GitLab.
    public sealed class TratadorDeWebhookGitLab(
        IProjetoDoWebhookQuery projetos,
        IAcaoBotRepositorio acoes,
        ConclusaoDaAcaoBot conclusao,
        IDescarteDoPainel painel,
        IPublicadorDeEventos eventos,
        ILogger<TratadorDeWebhookGitLab> log) : ITratadorDeWebhookGitLab
    {
        public const string EventoPipeline = "Pipeline Hook";
        public const string EventoMergeRequest = "Merge Request Hook";

        private const string CodigoPipelineFalhou = "preview.pipeline-falhou";

        private static readonly Erro CorpoInvalido = new(
            "webhook.corpo-invalido", "O corpo do webhook não é um evento válido do GitLab.", TipoErro.Validation);

        public async Task<Result> TratarAsync(string? evento, string corpo, CancellationToken cancellationToken = default)
        {
            if (evento is not (EventoPipeline or EventoMergeRequest))
                return Result.Sucesso();

            try
            {
                using var documento = JsonDocument.Parse(corpo);
                var raiz = documento.RootElement;
                if (raiz.ValueKind != JsonValueKind.Object)
                    return Result.Falha(CorpoInvalido);

                var caminho = Texto(raiz, "project", "path_with_namespace");
                if (string.IsNullOrWhiteSpace(caminho))
                    return Result.Falha(CorpoInvalido);

                // Projeto não cadastrado: mesma resposta de "tratado", para o hook não servir de sonda de cadastro.
                var projeto = await projetos.ObterAtivoAsync(caminho, cancellationToken);
                if (projeto is null)
                    return Result.Sucesso();

                return evento == EventoPipeline
                    ? await PipelineAsync(raiz, projeto, cancellationToken)
                    : await MergeRequestAsync(raiz, projeto, cancellationToken);
            }
            catch (JsonException)
            {
                return Result.Falha(CorpoInvalido);
            }
        }

        private async Task<Result> PipelineAsync(JsonElement raiz, ProjetoDoWebhook projeto, CancellationToken ct)
        {
            if (!raiz.TryGetProperty("object_attributes", out var atributos) || atributos.ValueKind != JsonValueKind.Object
                || !atributos.TryGetProperty("id", out var idJson) || !idJson.TryGetInt64(out var pipelineId))
                return Result.Falha(CorpoInvalido);

            var status = Texto(atributos, "status") ?? string.Empty;

            // Conclui antes de publicar: quando o front recebe o evento, a Ação já está gravada.
            await ConcluirAcaoAsync(projeto, pipelineId, status, ct);

            await eventos.PublicarAsync(new PipelineAtualizada(
                projeto.AplicacaoPublicId, projeto.ProjetoPublicId, pipelineId, status,
                Texto(atributos, "ref") ?? string.Empty, Texto(atributos, "sha") ?? string.Empty), ct);

            return Result.Sucesso();
        }

        // Só a Ação `abrir-review` em `executando` com esta pipeline. A segunda conclusão (webhook + consulta) é no-op: a lista
        // só traz `executando`, a transição inválida volta como Result e o conflito do banco é engolido aqui.
        private async Task ConcluirAcaoAsync(ProjetoDoWebhook projeto, long pipelineId, string status, CancellationToken ct)
        {
            var verde = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
            var vermelho = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase);
            if (!verde && !vermelho)
                return;

            var emExecucao = await acoes.ListarEmExecucaoPorTipoAsync(TipoAcaoBot.AbrirReview, ct);
            var acao = emExecucao.FirstOrDefault(a => a.ProjetoId == projeto.ProjetoId && a.PipelineId == pipelineId);
            if (acao is null)
                return;

            try
            {
                if (verde)
                    await conclusao.ConfirmarAsync(acao, ct);
                else
                    await conclusao.FalharAsync(acao, CodigoPipelineFalhou, ct);
            }
            catch (ConcorrenciaOtimistaException)
            {
                log.LogInformation("Ação {Acao}: já concluída por outro caminho; webhook ignorado.", acao.PublicId);
            }
        }

        private async Task<Result> MergeRequestAsync(JsonElement raiz, ProjetoDoWebhook projeto, CancellationToken ct)
        {
            var branch = raiz.TryGetProperty("object_attributes", out var atributos) && atributos.ValueKind == JsonValueKind.Object
                ? Texto(atributos, "source_branch")
                : null;

            // Para a próxima leitura do Painel ser fresca, sem esperar os 60 s do guardado.
            await painel.DescartarAsync(projeto.AplicacaoPublicId.ToString(), ct);
            await eventos.PublicarAsync(new FatiasMudaram(projeto.AplicacaoPublicId, CodigoDaFatia(branch)), ct);
            return Result.Sucesso();
        }

        // `feature/<fatia minúscula>-<slug>` vira o código do mapa com a primeira letra maiúscula (t1 -> T1, p2b -> P2b).
        private static string? CodigoDaFatia(string? branch)
        {
            var codigo = LeitorDeBranch.Ler(branch)?.Codigo;
            return string.IsNullOrEmpty(codigo) ? null : char.ToUpperInvariant(codigo[0]) + codigo[1..];
        }

        private static string? Texto(JsonElement elemento, params string[] caminho)
        {
            foreach (var parte in caminho)
            {
                if (elemento.ValueKind != JsonValueKind.Object || !elemento.TryGetProperty(parte, out elemento))
                    return null;
            }

            return elemento.ValueKind == JsonValueKind.String ? elemento.GetString() : null;
        }
    }
}
