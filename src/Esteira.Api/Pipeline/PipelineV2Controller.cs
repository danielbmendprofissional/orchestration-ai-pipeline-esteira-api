using Esteira.Api.Erros;
using Esteira.Application.Analises.Gate;
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
    public class PipelineV2Controller(PorteiroDaPipeline porteiro, IAbrirPipeline abrir, IGateDaPipeline gate) : ControllerBase
    {
        public const string DocumentoOpenApi = "pipeline-v2";

        private const int TamanhoMaximoDoCorpo = 64 * 1024;
        // `esteira-gate` leva Achados de varias ferramentas: ate 2 MB (spec E2).
        private const int TamanhoMaximoDoCorpoDoGate = 2 * 1024 * 1024;

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
            [property: JsonProperty("destinos"), System.Text.Json.Serialization.JsonPropertyName("destinos")] IReadOnlyList<DestinoDoAbrir> Destinos,
            [property: JsonProperty("teto_custo_usd"), System.Text.Json.Serialization.JsonPropertyName("teto_custo_usd")] decimal TetoCustoUsd);

        public sealed class CorpoDoGate
        {
            [JsonProperty("process"), System.Text.Json.Serialization.JsonPropertyName("process")] public string? Process { get; set; }
            [JsonProperty("env"), System.Text.Json.Serialization.JsonPropertyName("env")] public string? Env { get; set; }
            [JsonProperty("entrega_id"), System.Text.Json.Serialization.JsonPropertyName("entrega_id")] public string? EntregaId { get; set; }
            [JsonProperty("fluxo_id"), System.Text.Json.Serialization.JsonPropertyName("fluxo_id")] public string? FluxoId { get; set; }
            [JsonProperty("sha"), System.Text.Json.Serialization.JsonPropertyName("sha")] public string? Sha { get; set; }
            [JsonProperty("projeto"), System.Text.Json.Serialization.JsonPropertyName("projeto")] public string? Projeto { get; set; }
            [JsonProperty("pipeline_id"), System.Text.Json.Serialization.JsonPropertyName("pipeline_id")] public string? PipelineId { get; set; }
            [JsonProperty("analises"), System.Text.Json.Serialization.JsonPropertyName("analises")] public List<AnaliseDoGate?>? Analises { get; set; }

            internal GateCorpo ParaEntrada() => new(
                Process, Env, EntregaId, FluxoId, Sha, Projeto, PipelineId,
                Analises?.Select(a => a is null ? null! : new GateAnaliseCorpo(
                    a.Fonte, a.Tipo, a.Escopo, a.Veredito, a.Destino, a.CustoUsd,
                    a.Achados?.Select(x => x is null ? null! : new GateAchadoCorpo(x.Severidade, x.Titulo, x.RegraId, x.Arquivo, x.Linha, x.Descricao)).ToList()))
                    .ToList());
        }

        public sealed class AnaliseDoGate
        {
            [JsonProperty("fonte"), System.Text.Json.Serialization.JsonPropertyName("fonte")] public string? Fonte { get; set; }
            [JsonProperty("tipo"), System.Text.Json.Serialization.JsonPropertyName("tipo")] public string? Tipo { get; set; }
            [JsonProperty("escopo"), System.Text.Json.Serialization.JsonPropertyName("escopo")] public string? Escopo { get; set; }
            [JsonProperty("veredito"), System.Text.Json.Serialization.JsonPropertyName("veredito")] public string? Veredito { get; set; }
            [JsonProperty("destino"), System.Text.Json.Serialization.JsonPropertyName("destino")] public string? Destino { get; set; }
            [JsonProperty("custo_usd"), System.Text.Json.Serialization.JsonPropertyName("custo_usd")] public decimal? CustoUsd { get; set; }
            [JsonProperty("achados"), System.Text.Json.Serialization.JsonPropertyName("achados")] public List<AchadoDoGate?>? Achados { get; set; }
        }

        public sealed class AchadoDoGate
        {
            [JsonProperty("severidade"), System.Text.Json.Serialization.JsonPropertyName("severidade")] public string? Severidade { get; set; }
            [JsonProperty("titulo"), System.Text.Json.Serialization.JsonPropertyName("titulo")] public string? Titulo { get; set; }
            [JsonProperty("regra_id"), System.Text.Json.Serialization.JsonPropertyName("regra_id")] public string? RegraId { get; set; }
            [JsonProperty("arquivo"), System.Text.Json.Serialization.JsonPropertyName("arquivo")] public string? Arquivo { get; set; }
            [JsonProperty("linha"), System.Text.Json.Serialization.JsonPropertyName("linha")] public int? Linha { get; set; }
            [JsonProperty("descricao"), System.Text.Json.Serialization.JsonPropertyName("descricao")] public string? Descricao { get; set; }
        }

        public sealed record ContagemDoGate(
            [property: JsonProperty("critica"), System.Text.Json.Serialization.JsonPropertyName("critica")] int Critica,
            [property: JsonProperty("alta"), System.Text.Json.Serialization.JsonPropertyName("alta")] int Alta,
            [property: JsonProperty("media"), System.Text.Json.Serialization.JsonPropertyName("media")] int Media,
            [property: JsonProperty("baixa"), System.Text.Json.Serialization.JsonPropertyName("baixa")] int Baixa);

        public sealed record RespostaDoGate(
            [property: JsonProperty("decisao"), System.Text.Json.Serialization.JsonPropertyName("decisao")] string Decisao,
            [property: JsonProperty("contagem"), System.Text.Json.Serialization.JsonPropertyName("contagem")] ContagemDoGate Contagem);

        // E2.2: Analises da pipeline. Qualquer 4xx reprova a pipeline; sem flag (um gate "desligado" seria bypass).
        [HttpPost("gate")]
        [Consumes("application/json")]
        [ProducesResponseType<RespostaDoGate>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<Result<RespostaDoGate>> Gate(CancellationToken cancellationToken)
        {
            var corpo = await LerJsonAsync<CorpoDoGate>(TamanhoMaximoDoCorpoDoGate, cancellationToken);
            var projetoDoToken = User.FindFirst(PipelineAutenticacao.ClaimProjeto)?.Value;

            var resposta = await gate.ExecutarAsync(projetoDoToken, corpo?.ParaEntrada(), cancellationToken);
            if (!resposta.IsSuccess)
                return Result<RespostaDoGate>.Falha(resposta.Errors.ToArray());

            var c = resposta.Value.Contagem;
            return Result<RespostaDoGate>.Sucesso(new RespostaDoGate(resposta.Value.Decisao, new ContagemDoGate(c.Critica, c.Alta, c.Media, c.Baixa)));
        }

        // JSON ilegivel, vazio ou acima do limite = nulo (o handler responde 422 pipeline.corpo-invalido).
        private async Task<T?> LerJsonAsync<T>(int limite, CancellationToken cancellationToken) where T : class
        {
            if (Request.ContentLength > limite)
                return null;

            try
            {
                using var leitor = new StreamReader(Request.Body);
                var buffer = new char[limite + 1];
                var lidos = await leitor.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
                if (lidos is 0 || lidos > limite)
                    return null;
                return JsonConvert.DeserializeObject<T>(new string(buffer, 0, lidos));
            }
            catch (JsonException)
            {
                return null;
            }
        }

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
                resposta.Value.Destinos.Select(d => new DestinoDoAbrir(d.Nome, d.Ordem, d.Configuracao)).ToList(),
                resposta.Value.TetoCustoUsd));
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
