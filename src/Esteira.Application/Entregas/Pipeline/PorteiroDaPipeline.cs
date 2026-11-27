using System.Text.RegularExpressions;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Entregas.Pipeline
{
    // Corpo como chegou, campo a campo (nulo = ausente). Vem do JSON do cliente (template-ci, contrato-pipeline-v2.md).
    public sealed record AbrirPipelineCorpo(
        string? Process,
        string? Env,
        string? EntregaId,
        string? FluxoId,
        string? Sha,
        string? Projeto,
        string? PipelineId,
        string? Versao,
        string? AtalhoPor,
        string? AtalhoMotivo,
        bool? FlagSomenteLeitura);

    // Passos 1 (Projeto) e 2 (corpo) do `esteira-abrir`. O id_token já foi autenticado pela Api (401); aqui entra o que
    // vem depois dele. Nada daqui vai ao banco antes de o corpo ser válido: a consulta de Projeto é o último passo.
    public sealed partial class PorteiroDaPipeline(IProjetoDaPipelineQuery projetos)
    {
        // 403 único para "o corpo diz outro Projeto", "não cadastrado" e "inativo": não revela se o Projeto existe.
        public static readonly Erro ProjetoNaoReconhecido = new(
            "pipeline.projeto-nao-reconhecido", "O Projeto não é reconhecido pela Esteira.", TipoErro.Forbidden);

        public static readonly Erro CorpoInvalido = new(
            "pipeline.corpo-invalido", "O corpo da requisição é inválido.", TipoErro.Failure);

        private static readonly HashSet<string> Processos = ["fluxo", "promocao", "liberacao", "flag", "rollback", "rotulo"];
        private static readonly HashSet<string> Ambientes = ["qa", "hml", "production"];

        [GeneratedRegex("^[0-9a-f]{40}$")]
        private static partial Regex ShaValido();

        public async Task<Result<AbrirPipelineEntrada>> AdmitirAsync(
            string? projetoDoToken, AbrirPipelineCorpo? corpo, CancellationToken cancellationToken = default)
        {
            if (corpo is null)
                return Result<AbrirPipelineEntrada>.Falha(CorpoInvalido);

            // O `projeto` do corpo só é aceito depois de igual ao do token.
            if (string.IsNullOrWhiteSpace(projetoDoToken) || !string.Equals(corpo.Projeto, projetoDoToken, StringComparison.Ordinal))
                return Result<AbrirPipelineEntrada>.Falha(ProjetoNaoReconhecido);

            var entrada = Validar(corpo);
            if (entrada is null)
                return Result<AbrirPipelineEntrada>.Falha(CorpoInvalido);

            if (!await projetos.ProjetoAtivoCadastradoAsync(projetoDoToken, cancellationToken))
                return Result<AbrirPipelineEntrada>.Falha(ProjetoNaoReconhecido);

            return Result<AbrirPipelineEntrada>.Sucesso(entrada);
        }

        private static AbrirPipelineEntrada? Validar(AbrirPipelineCorpo c)
        {
            if (c.Process is null || !Processos.Contains(c.Process))
                return null;

            // `env` é vazio só no `rotulo` (contrato).
            var envVazioDoRotulo = c.Process == "rotulo" && string.IsNullOrEmpty(c.Env);
            if (!envVazioDoRotulo && (c.Env is null || !Ambientes.Contains(c.Env)))
                return null;

            if (c.Sha is null || !ShaValido().IsMatch(c.Sha))
                return null;

            if (!Guid.TryParseExact(c.EntregaId, "D", out var entrega) || !Guid.TryParseExact(c.FluxoId, "D", out var fluxo))
                return null;

            return new AbrirPipelineEntrada(
                c.Process, c.Env ?? string.Empty, entrega, fluxo, c.Sha, c.Projeto!, c.PipelineId, c.Versao,
                c.AtalhoPor, c.AtalhoMotivo, c.FlagSomenteLeitura ?? false);
        }
    }

    // Porta de leitura: o caminho do GitLab (`project_path`) é de um Projeto ativo cadastrado, em qualquer Aplicação.
    public interface IProjetoDaPipelineQuery
    {
        Task<bool> ProjetoAtivoCadastradoAsync(string caminho, CancellationToken cancellationToken = default);
    }
}
