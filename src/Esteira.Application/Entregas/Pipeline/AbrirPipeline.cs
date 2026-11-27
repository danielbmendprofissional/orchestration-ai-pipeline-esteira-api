using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Entregas.Pipeline
{
    // Entrada do `esteira-abrir` já validada (passo 2): o que a E1.4 recebe. `Projeto` é o caminho já igualado ao
    // `project_path` do id_token e conferido como Projeto ativo cadastrado (passo 1).
    public sealed record AbrirPipelineEntrada(
        string Process,
        string Env,
        Guid EntregaId,
        Guid FluxoId,
        string Sha,
        string Projeto,
        string? PipelineId,
        string? Versao,
        string? AtalhoPor,
        string? AtalhoMotivo,
        bool FlagSomenteLeitura);

    public sealed record DestinoNaResposta(string Nome, int Ordem, IReadOnlyDictionary<string, string> Configuracao);

    public sealed record AbrirPipelineResposta(string FeatureFlags, IReadOnlyList<DestinoNaResposta> Destinos);

    // PONTO DE EXTENSÃO DA E1.4: passos 3–9 do `esteira-abrir` (processo suportado, Entrega e Fluxo, Ambiente, SHA, Atalho,
    // ocupação, resposta). A E1.4 troca o registro desta porta em Esteira.IoC (hoje `AbrirPipelineNaoImplementado`).
    public interface IAbrirPipeline
    {
        Task<Result<AbrirPipelineResposta>> ExecutarAsync(AbrirPipelineEntrada entrada, CancellationToken cancellationToken = default);
    }

    // Fail-closed até a E1.4: nenhuma chamada autenticada e válida é aceita "pela metade", nem recebe sucesso falso.
    public sealed class AbrirPipelineNaoImplementado : IAbrirPipeline
    {
        public Task<Result<AbrirPipelineResposta>> ExecutarAsync(AbrirPipelineEntrada entrada, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AbrirPipelineResposta>.Falha(new Erro(
                "pipeline.nao-implementado", "A abertura do Artefato ainda não está disponível nesta versão da Esteira.", TipoErro.Failure)));
    }
}
