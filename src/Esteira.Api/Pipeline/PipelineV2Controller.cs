using Esteira.Api.Erros;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Esteira.Api.Pipeline
{
    // Superfície da pipeline (ADR-0013): documento OpenAPI próprio `pipeline-v2`, fora do `v1` do front.
    // Contrato: template-ci/docs/contrato-pipeline-v2.md. Qualquer resposta diferente de 200 reprova a pipeline.
    [ApiController]
    [Route("api/pipeline/v2")]
    [ApiExplorerSettings(GroupName = DocumentoOpenApi)]
    [Authorize(AuthenticationSchemes = PipelineAutenticacao.Esquema)]
    public class PipelineV2Controller(PorteiroDaPipeline porteiro, IAbrirPipeline abrir) : ControllerBase
    {
        public const string DocumentoOpenApi = "pipeline-v2";

        private const int TamanhoMaximoDoCorpo = 64 * 1024;

        public sealed class CorpoDoAbrir
        {
            [JsonProperty("process"), System.Text.Json.Serialization.JsonPropertyName("process")] public string? Process { get; set; }
            [JsonProperty("env"), System.Text.Json.Serialization.JsonPropertyName("env")] public string? Env { get; set; }
            [JsonProperty("entrega_id"), System.Text.Json.Serialization.JsonPropertyName("entrega_id")] public string? EntregaId { get; set; }
            [JsonProperty("fluxo_id"), System.Text.Json.Serialization.JsonPropertyName("fluxo_id")] public string? FluxoId { get; set; }
            [JsonProperty("sha"), System.Text.Json.Serialization.JsonPropertyName("sha")] public string? Sha { get; set; }
            [JsonProperty("projeto"), System.Text.Json.Serialization.JsonPropertyName("projeto")] public string? Projeto { get; set; }
            [JsonProperty("pipeline_id"), System.Text.Json.Serialization.JsonPropertyName("pipeline_id")] public string? PipelineId { get; set; }
            [JsonProperty("versao"), System.Text.Json.Serialization.JsonPropertyName("versao")] public string? Versao { get; set; }
            [JsonProperty("atalho_por"), System.Text.Json.Serialization.JsonPropertyName("atalho_por")] public string? AtalhoPor { get; set; }
            [JsonProperty("atalho_motivo"), System.Text.Json.Serialization.JsonPropertyName("atalho_motivo")] public string? AtalhoMotivo { get; set; }
            [JsonProperty("flag_somente_leitura"), System.Text.Json.Serialization.JsonPropertyName("flag_somente_leitura")] public bool? FlagSomenteLeitura { get; set; }
        }

        public sealed record DestinoDoAbrir(
            [property: JsonProperty("nome"), System.Text.Json.Serialization.JsonPropertyName("nome")] string Nome,
            [property: JsonProperty("ordem"), System.Text.Json.Serialization.JsonPropertyName("ordem")] int Ordem,
            [property: JsonProperty("configuracao"), System.Text.Json.Serialization.JsonPropertyName("configuracao")] IReadOnlyDictionary<string, string> Configuracao);

        public sealed record RespostaDoAbrir(
            [property: JsonProperty("feature_flags"), System.Text.Json.Serialization.JsonPropertyName("feature_flags")] string FeatureFlags,
            [property: JsonProperty("destinos"), System.Text.Json.Serialization.JsonPropertyName("destinos")] IReadOnlyList<DestinoDoAbrir> Destinos);

        // Passos 1 e 2 aqui (id_token já autenticado pelo esquema; Projeto; corpo); 3–9 na porta `IAbrirPipeline` (E1.4).
        // O corpo é lido à mão para que JSON ilegível seja 422 pipeline.corpo-invalido, não o 400 genérico do model binding.
        [HttpPost("abrir")]
        [Consumes("application/json")]
        [ProducesResponseType<RespostaDoAbrir>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<Result<RespostaDoAbrir>> Abrir(CancellationToken cancellationToken)
        {
            var corpo = await LerCorpoAsync(cancellationToken);
            var projetoDoToken = User.FindFirst(PipelineAutenticacao.ClaimProjeto)?.Value;

            var admitida = await porteiro.AdmitirAsync(projetoDoToken, corpo, cancellationToken);
            if (!admitida.IsSuccess)
                return Result<RespostaDoAbrir>.Falha(admitida.Errors.ToArray());

            var resposta = await abrir.ExecutarAsync(admitida.Value, cancellationToken);
            if (!resposta.IsSuccess)
                return Result<RespostaDoAbrir>.Falha(resposta.Errors.ToArray());

            return Result<RespostaDoAbrir>.Sucesso(new RespostaDoAbrir(
                resposta.Value.FeatureFlags,
                resposta.Value.Destinos.Select(d => new DestinoDoAbrir(d.Nome, d.Ordem, d.Configuracao)).ToList()));
        }

        private async Task<AbrirPipelineCorpo?> LerCorpoAsync(CancellationToken cancellationToken)
        {
            if (Request.ContentLength is > TamanhoMaximoDoCorpo)
                return null;

            try
            {
                using var leitor = new StreamReader(Request.Body);
                var buffer = new char[TamanhoMaximoDoCorpo + 1];
                var lidos = await leitor.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
                if (lidos is 0 or > TamanhoMaximoDoCorpo)
                    return null;

                var c = JsonConvert.DeserializeObject<CorpoDoAbrir>(new string(buffer, 0, lidos));
                return c is null
                    ? null
                    : new AbrirPipelineCorpo(
                        c.Process, c.Env, c.EntregaId, c.FluxoId, c.Sha, c.Projeto, c.PipelineId, c.Versao,
                        c.AtalhoPor, c.AtalhoMotivo, c.FlagSomenteLeitura);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
