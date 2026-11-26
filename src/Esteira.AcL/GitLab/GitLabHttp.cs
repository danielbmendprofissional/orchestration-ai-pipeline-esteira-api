using System.Net;
using System.Text;
using System.Text.Json;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esteira.AcL.GitLab
{
    // Configuração: `GitLab:Url` e `GitLab:Bots:<caminho-do-grupo>:Token` (só user-secrets/Secret).
    public sealed class GitLabOptions
    {
        public const string Secao = "GitLab";

        public string? Url { get; set; }

        // Chave = caminho do grupo (sem diferenciar maiúsculas).
        public Dictionary<string, GitLabBotOptions> Bots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class GitLabBotOptions
    {
        public string? Token { get; set; }
    }

    // Adaptador da porta IGitLab sobre a REST v4: leitura (GET) e escrita da Ação do bot. O token do bot vai só no
    // header PRIVATE-TOKEN de cada pedido; o token de trigger criado na hora vai só no corpo do disparo da pipeline.
    // Nenhum dos dois entra em mensagem de erro, retorno ou log.
    public sealed class GitLabHttp(HttpClient http, IOptions<GitLabOptions> opcoes, ILogger<GitLabHttp> log) : IGitLab
    {
        private const int PorPagina = 100;
        private const int MaximoDePaginas = 50;
        private const int TituloMaximo = 200;
        private const int DescricaoMaxima = 2000;
        private const int DescricaoDaMaeMaxima = 10000;

        private static readonly Erro BotNaoConfigurado = new(
            "gitlab.bot-nao-configurado", "O bot do GitLab não está configurado para este grupo.", TipoErro.Failure);

        private static readonly Erro GrupoNaoEncontrado = new(
            "gitlab.grupo-nao-encontrado", "Grupo não encontrado no GitLab, ou o bot não tem acesso a ele.", TipoErro.Failure);

        private static readonly Erro Indisponivel = new(
            "gitlab.indisponivel", "Não foi possível consultar o GitLab agora. Tente novamente.", TipoErro.Failure);

        private static readonly Erro SemPermissao = new(
            "gitlab.sem-permissao", "O bot do GitLab não tem permissão para esta operação.", TipoErro.Forbidden);

        private static readonly Erro MrNaoEncontrada = new(
            "gitlab.mr-nao-encontrada", "A merge request não foi encontrada no GitLab.", TipoErro.NotFound);

        private static readonly Erro MergeRecusado = new(
            "gitlab.merge-recusado", "O GitLab recusou o merge (conflito ou regra do projeto).", TipoErro.Conflict);

        public async Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default)
        {
            var resposta = await EnviarAsync(caminho, $"api/v4/groups/{Uri.EscapeDataString(caminho)}", cancellationToken);
            if (resposta.Erro is not null)
                return Result<GrupoGitLabDto>.Falha(resposta.Erro);

            using var _ = resposta.Mensagem!;
            if (resposta.Mensagem!.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                return Result<GrupoGitLabDto>.Falha(GrupoNaoEncontrado);

            return await LerAsync(resposta.Mensagem, async corpo =>
            {
                using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                var raiz = doc.RootElement;
                return Result<GrupoGitLabDto>.Sucesso(new GrupoGitLabDto(
                    raiz.GetProperty("id").GetInt64(),
                    raiz.GetProperty("full_path").GetString()!,
                    raiz.GetProperty("name").GetString()!));
            }, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(
            string grupoCaminho, long grupoId, CancellationToken cancellationToken = default)
        {
            var projetos = new List<ProjetoGitLabDto>();
            var pagina = 1;

            while (pagina <= MaximoDePaginas)
            {
                var url = $"api/v4/groups/{grupoId}/projects?include_subgroups=true&archived=false&per_page={PorPagina}&page={pagina}";
                var resposta = await EnviarAsync(grupoCaminho, url, cancellationToken);
                if (resposta.Erro is not null)
                    return Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(resposta.Erro);

                using var mensagem = resposta.Mensagem!;
                if (mensagem.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                    return Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(GrupoNaoEncontrado);
                if (!mensagem.IsSuccessStatusCode)
                    return Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(ErroDe(mensagem.StatusCode));

                try
                {
                    await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                    foreach (var item in doc.RootElement.EnumerateArray())
                        projetos.Add(new ProjetoGitLabDto(
                            item.GetProperty("id").GetInt64(),
                            item.GetProperty("path_with_namespace").GetString()!,
                            item.GetProperty("name").GetString()!,
                            item.TryGetProperty("default_branch", out var branch) && branch.ValueKind == JsonValueKind.String
                                ? branch.GetString()
                                : null));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    log.LogWarning("Resposta do GitLab fora do formato esperado ao listar projetos ({Tipo}).", ex.GetType().Name);
                    return Result<IReadOnlyList<ProjetoGitLabDto>>.Falha(Indisponivel);
                }

                var proxima = mensagem.Headers.TryGetValues("X-Next-Page", out var valores) ? valores.FirstOrDefault() : null;
                if (!int.TryParse(proxima, out var numero) || numero <= pagina)
                    break;
                pagina = numero;
            }

            return Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso(projetos);
        }

        public async Task<Result<string?>> ObterArquivoAsync(
            string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default)
        {
            var url = $"api/v4/projects/{projetoId}/repository/files/{Uri.EscapeDataString(caminho)}/raw?ref={Uri.EscapeDataString(referencia)}";
            var resposta = await EnviarAsync(grupoCaminho, url, cancellationToken);
            if (resposta.Erro is not null)
                return Result<string?>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (mensagem.StatusCode == HttpStatusCode.NotFound)
                return Result<string?>.Sucesso(null);
            if (!mensagem.IsSuccessStatusCode)
                return Result<string?>.Falha(ErroDe(mensagem.StatusCode));

            return Result<string?>.Sucesso(await mensagem.Content.ReadAsStringAsync(cancellationToken));
        }

        public Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
            => ListarPaginadoAsync(grupoCaminho,
                pagina => $"api/v4/projects/{projetoId}/issues?state=all&per_page={PorPagina}&page={pagina}",
                item => new IssueGitLabDto(
                    item.GetProperty("iid").GetInt64(),
                    Cortar(item.GetProperty("title").GetString(), TituloMaximo)!,
                    item.GetProperty("state").GetString()!,
                    item.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Array
                        ? l.EnumerateArray().Select(x => x.GetString()!).ToList()
                        : [],
                    DescricaoDaMae(item)),
                cancellationToken);

        public Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
            => ListarPaginadoAsync(grupoCaminho,
                pagina => $"api/v4/projects/{projetoId}/merge_requests?target_branch=main&state=all&per_page={PorPagina}&page={pagina}",
                item => new MergeRequestGitLabDto(
                    item.GetProperty("iid").GetInt64(),
                    Cortar(item.GetProperty("title").GetString(), TituloMaximo)!,
                    item.GetProperty("state").GetString()!,
                    item.GetProperty("source_branch").GetString()!,
                    item.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                        ? Cortar(d.GetString(), DescricaoMaxima) : null,
                    (item.TryGetProperty("head_pipeline", out var p) && p.ValueKind == JsonValueKind.Object
                        && p.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String)
                        ? st.GetString() : null),
                cancellationToken);

        public async Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
        {
            var resposta = await EnviarAsync(grupoCaminho, $"api/v4/projects/{projetoId}", cancellationToken);
            if (resposta.Erro is not null)
                return Result<ProjetoDoGrupoGitLabDto?>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (mensagem.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                return Result<ProjetoDoGrupoGitLabDto?>.Sucesso(null);

            return await LerAsync<ProjetoDoGrupoGitLabDto?>(mensagem, async corpo =>
            {
                using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                var raiz = doc.RootElement;
                var caminho = raiz.GetProperty("path_with_namespace").GetString()!;
                return Result<ProjetoDoGrupoGitLabDto?>.Sucesso(new ProjetoDoGrupoGitLabDto(
                    raiz.GetProperty("id").GetInt64(),
                    caminho,
                    raiz.GetProperty("name").GetString()!,
                    raiz.TryGetProperty("default_branch", out var branch) && branch.ValueKind == JsonValueKind.String
                        ? branch.GetString() : null,
                    caminho.StartsWith(grupoCaminho + "/", StringComparison.OrdinalIgnoreCase)));
            }, cancellationToken);
        }

        // ---- Escrita como Ação do bot (P2b) ----

        public async Task<Result<MergeRequestDaAcaoGitLabDto>> ObterMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Get, $"api/v4/projects/{projetoId}/merge_requests/{mrIid}", null, cancellationToken);
            if (resposta.Erro is not null)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(ErroDeMr(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, MapearMr, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<string>>> ListarNotasMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default)
        {
            var notas = new List<string>();
            var pagina = 1;

            while (pagina <= MaximoDePaginas)
            {
                var resposta = await ExecutarAsync(
                    grupoCaminho, HttpMethod.Get,
                    $"api/v4/projects/{projetoId}/merge_requests/{mrIid}/notes?per_page={PorPagina}&page={pagina}",
                    null, cancellationToken);
                if (resposta.Erro is not null)
                    return Result<IReadOnlyList<string>>.Falha(resposta.Erro);

                using var mensagem = resposta.Mensagem!;
                if (!mensagem.IsSuccessStatusCode)
                    return Result<IReadOnlyList<string>>.Falha(ErroDeMr(mensagem.StatusCode));

                try
                {
                    await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                    foreach (var item in doc.RootElement.EnumerateArray())
                        notas.Add(item.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                            ? body.GetString() ?? string.Empty : string.Empty);
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    log.LogWarning("Resposta do GitLab fora do formato esperado ao listar notas ({Tipo}).", ex.GetType().Name);
                    return Result<IReadOnlyList<string>>.Falha(Indisponivel);
                }

                var proxima = mensagem.Headers.TryGetValues("X-Next-Page", out var valores) ? valores.FirstOrDefault() : null;
                if (!int.TryParse(proxima, out var numero) || numero <= pagina)
                    break;
                pagina = numero;
            }

            return Result<IReadOnlyList<string>>.Sucesso(notas);
        }

        public async Task<Result<long>> ComentarMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, string corpo, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Post, $"api/v4/projects/{projetoId}/merge_requests/{mrIid}/notes",
                CorpoJson(new { body = corpo }), cancellationToken);
            if (resposta.Erro is not null)
                return Result<long>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<long>.Falha(ErroDeMr(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, raiz => raiz.GetProperty("id").GetInt64(), cancellationToken);
        }

        public Task<Result<MergeRequestDaAcaoGitLabDto>> FecharMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default)
            => AlterarEstadoMrAsync(grupoCaminho, projetoId, mrIid, "close", cancellationToken: cancellationToken);

        public Task<Result<MergeRequestDaAcaoGitLabDto>> ReabrirMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default)
            => AlterarEstadoMrAsync(grupoCaminho, projetoId, mrIid, "reopen", cancellationToken: cancellationToken);

        public async Task<Result<MergeRequestDaAcaoGitLabDto>> MergearMergeRequestAsync(
            string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default)
        {
            // Merge commit, sem squash e sem apagar a branch de origem (ADR-0021): a branch da MR é reaproveitada.
            var corpo = CorpoJson(new { squash = false, should_remove_source_branch = false });
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Put, $"api/v4/projects/{projetoId}/merge_requests/{mrIid}/merge", corpo, cancellationToken);
            if (resposta.Erro is not null)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(ErroDeMerge(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, MapearMr, cancellationToken);
        }

        public async Task<Result<TriggerTokenGitLabDto>> CriarTriggerTokenAsync(
            string grupoCaminho, long projetoId, string descricao, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Post, $"api/v4/projects/{projetoId}/triggers",
                CorpoJson(new { description = descricao }), cancellationToken);
            if (resposta.Erro is not null)
                return Result<TriggerTokenGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<TriggerTokenGitLabDto>.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, raiz => new TriggerTokenGitLabDto(
                raiz.GetProperty("id").GetInt64(), raiz.GetProperty("token").GetString()!), cancellationToken);
        }

        public async Task<Result> ApagarTriggerTokenAsync(
            string grupoCaminho, long projetoId, long triggerTokenId, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Delete, $"api/v4/projects/{projetoId}/triggers/{triggerTokenId}", null, cancellationToken);
            if (resposta.Erro is not null)
                return Result.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            // 404 = o token já não existe; apagar é idempotente, então é sucesso.
            if (mensagem.StatusCode == HttpStatusCode.NotFound || mensagem.IsSuccessStatusCode)
                return Result.Sucesso();

            return Result.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));
        }

        public async Task<Result<long>> CriarPipelinePorTriggerAsync(
            string grupoCaminho, long projetoId, string triggerToken, string referencia,
            IReadOnlyDictionary<string, string> variaveis, CancellationToken cancellationToken = default)
        {
            var campos = new List<KeyValuePair<string, string>>
            {
                new("token", triggerToken),
                new("ref", referencia),
            };
            campos.AddRange(variaveis.Select(v => new KeyValuePair<string, string>($"variables[{v.Key}]", v.Value)));

            // O trigger token autentica o disparo pelo corpo; o token do bot não entra nesta chamada.
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Post, $"api/v4/projects/{projetoId}/trigger/pipeline",
                new FormUrlEncodedContent(campos), cancellationToken, exigeBot: false);
            if (resposta.Erro is not null)
                return Result<long>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<long>.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, raiz => raiz.GetProperty("id").GetInt64(), cancellationToken);
        }

        public async Task<Result<PipelineGitLabDto>> ObterPipelineAsync(
            string grupoCaminho, long projetoId, long pipelineId, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Get, $"api/v4/projects/{projetoId}/pipelines/{pipelineId}", null, cancellationToken);
            if (resposta.Erro is not null)
                return Result<PipelineGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<PipelineGitLabDto>.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, MapearPipeline, cancellationToken);
        }

        public async Task<Result<PipelineGitLabDto>> CancelarPipelineAsync(
            string grupoCaminho, long projetoId, long pipelineId, CancellationToken cancellationToken = default)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Post, $"api/v4/projects/{projetoId}/pipelines/{pipelineId}/cancel", null, cancellationToken);
            if (resposta.Erro is not null)
                return Result<PipelineGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<PipelineGitLabDto>.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, MapearPipeline, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>> ListarTriggerTokensAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
        {
            var tokens = new List<TriggerTokenListadoGitLabDto>();
            var pagina = 1;

            while (pagina <= MaximoDePaginas)
            {
                var resposta = await ExecutarAsync(
                    grupoCaminho, HttpMethod.Get,
                    $"api/v4/projects/{projetoId}/triggers?per_page={PorPagina}&page={pagina}", null, cancellationToken);
                if (resposta.Erro is not null)
                    return Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>.Falha(resposta.Erro);

                using var mensagem = resposta.Mensagem!;
                if (!mensagem.IsSuccessStatusCode)
                    return Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>.Falha(ErroDeEscritaDoProjeto(mensagem.StatusCode));

                try
                {
                    await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                    foreach (var item in doc.RootElement.EnumerateArray())
                        tokens.Add(new TriggerTokenListadoGitLabDto(
                            item.GetProperty("id").GetInt64(),
                            item.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                                ? d.GetString() ?? string.Empty : string.Empty,
                            item.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String
                                ? DateTimeOffset.Parse(c.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                                : DateTimeOffset.MinValue));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    log.LogWarning("Resposta do GitLab fora do formato esperado ao listar trigger tokens ({Tipo}).", ex.GetType().Name);
                    return Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>.Falha(Indisponivel);
                }

                var proxima = mensagem.Headers.TryGetValues("X-Next-Page", out var valores) ? valores.FirstOrDefault() : null;
                if (!int.TryParse(proxima, out var numero) || numero <= pagina)
                    break;
                pagina = numero;
            }

            return Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>.Sucesso(tokens);
        }

        private static PipelineGitLabDto MapearPipeline(JsonElement raiz)
            => new(raiz.GetProperty("id").GetInt64(), raiz.GetProperty("status").GetString()!);

        private async Task<Result<MergeRequestDaAcaoGitLabDto>> AlterarEstadoMrAsync(
            string grupoCaminho, long projetoId, long mrIid, string stateEvent, CancellationToken cancellationToken)
        {
            var resposta = await ExecutarAsync(
                grupoCaminho, HttpMethod.Put, $"api/v4/projects/{projetoId}/merge_requests/{mrIid}",
                CorpoJson(new { state_event = stateEvent }), cancellationToken);
            if (resposta.Erro is not null)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(resposta.Erro);

            using var mensagem = resposta.Mensagem!;
            if (!mensagem.IsSuccessStatusCode)
                return Result<MergeRequestDaAcaoGitLabDto>.Falha(ErroDeMr(mensagem.StatusCode));

            return await LerJsonAsync(mensagem, MapearMr, cancellationToken);
        }

        private static MergeRequestDaAcaoGitLabDto MapearMr(JsonElement raiz)
            => new(
                raiz.GetProperty("iid").GetInt64(),
                raiz.GetProperty("state").GetString()!,
                raiz.GetProperty("source_branch").GetString()!,
                raiz.TryGetProperty("sha", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null,
                (raiz.TryGetProperty("head_pipeline", out var p) && p.ValueKind == JsonValueKind.Object
                    && p.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String)
                    ? st.GetString() : null);

        private async Task<Result<T>> LerJsonAsync<T>(
            HttpResponseMessage mensagem, Func<JsonElement, T> mapear, CancellationToken cancellationToken)
        {
            try
            {
                await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                return Result<T>.Sucesso(mapear(doc.RootElement));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                log.LogWarning("Resposta do GitLab fora do formato esperado ({Tipo}).", ex.GetType().Name);
                return Result<T>.Falha(Indisponivel);
            }
        }

        // Erros de uma MR: 401/403 sem permissão, 404 não encontrada, o resto indisponível.
        private Erro ErroDeMr(HttpStatusCode status)
        {
            log.LogWarning("O GitLab respondeu {Status}.", (int)status);
            return status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SemPermissao,
                HttpStatusCode.NotFound => MrNaoEncontrada,
                _ => Indisponivel
            };
        }

        // Erros do merge: além dos da MR, 409/406/405/400/422 = merge recusado (conflito ou regra do projeto).
        private Erro ErroDeMerge(HttpStatusCode status)
        {
            log.LogWarning("O GitLab respondeu {Status} ao mergear.", (int)status);
            return status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SemPermissao,
                HttpStatusCode.NotFound => MrNaoEncontrada,
                HttpStatusCode.Conflict or HttpStatusCode.NotAcceptable or HttpStatusCode.MethodNotAllowed
                    or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => MergeRecusado,
                _ => Indisponivel
            };
        }

        // Erros dos endpoints de token/pipeline (que não são de MR): sem código próprio na tabela, os 4xx definitivos
        // caem no erro mais próximo, gitlab.indisponivel.
        private Erro ErroDeEscritaDoProjeto(HttpStatusCode status)
        {
            log.LogWarning("O GitLab respondeu {Status}.", (int)status);
            return status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SemPermissao,
                _ => Indisponivel
            };
        }

        // Só a issue com label `em-execucao` carrega a descrição (bloco de execução da mãe); as outras não pagam o tráfego.
        private static string? DescricaoDaMae(JsonElement item)
        {
            if (!item.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array
                || !labels.EnumerateArray().Any(l => l.GetString() == "em-execucao"))
                return null;

            return item.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? Cortar(d.GetString(), DescricaoDaMaeMaxima) : null;
        }

        private static string? Cortar(string? texto, int maximo)
            => texto is not null && texto.Length > maximo ? texto[..maximo] : texto;

        private async Task<Result<IReadOnlyList<T>>> ListarPaginadoAsync<T>(
            string grupoCaminho, Func<int, string> url, Func<JsonElement, T> mapear, CancellationToken cancellationToken)
        {
            var itens = new List<T>();
            var pagina = 1;

            while (pagina <= MaximoDePaginas)
            {
                var resposta = await EnviarAsync(grupoCaminho, url(pagina), cancellationToken);
                if (resposta.Erro is not null)
                    return Result<IReadOnlyList<T>>.Falha(resposta.Erro);

                using var mensagem = resposta.Mensagem!;
                if (!mensagem.IsSuccessStatusCode)
                    return Result<IReadOnlyList<T>>.Falha(ErroDe(mensagem.StatusCode));

                try
                {
                    await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                    foreach (var item in doc.RootElement.EnumerateArray())
                        itens.Add(mapear(item));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    log.LogWarning("Resposta do GitLab fora do formato esperado ao listar ({Tipo}).", ex.GetType().Name);
                    return Result<IReadOnlyList<T>>.Falha(Indisponivel);
                }

                var proxima = mensagem.Headers.TryGetValues("X-Next-Page", out var valores) ? valores.FirstOrDefault() : null;
                if (!int.TryParse(proxima, out var numero) || numero <= pagina)
                    break;
                pagina = numero;
            }

            return Result<IReadOnlyList<T>>.Sucesso(itens);
        }

        private readonly record struct Resposta(HttpResponseMessage? Mensagem, Erro? Erro);

        // Wrapper de leitura (P1/P2): mantém o comportamento anterior — 401 vira gitlab.bot-nao-configurado.
        private async Task<Resposta> EnviarAsync(string grupoCaminho, string urlRelativa, CancellationToken cancellationToken)
        {
            var resposta = await ExecutarAsync(grupoCaminho, HttpMethod.Get, urlRelativa, null, cancellationToken);
            if (resposta.Erro is not null)
                return resposta;

            if (resposta.Mensagem!.StatusCode == HttpStatusCode.Unauthorized)
            {
                resposta.Mensagem.Dispose();
                log.LogWarning("O GitLab recusou o token do bot do grupo {Grupo}.", grupoCaminho);
                return new Resposta(null, BotNaoConfigurado);
            }

            return resposta;
        }

        // Resolve o token do grupo (quando a chamada o exige) e envia com o método e o corpo informados. Devolve o Erro
        // pronto quando não dá para chamar (bot não configurado) ou quando a chamada falha na rede (indisponível).
        // O corpo pode ser JSON ou form-urlencoded; o caller monta o HttpContent.
        private async Task<Resposta> ExecutarAsync(
            string grupoCaminho, HttpMethod metodo, string urlRelativa, HttpContent? conteudo, CancellationToken cancellationToken,
            bool exigeBot = true)
        {
            string? token = null;
            if (exigeBot)
            {
                token = opcoes.Value.Bots.TryGetValue(grupoCaminho, out var bot) ? bot.Token : null;
                if (string.IsNullOrWhiteSpace(token) || http.BaseAddress is null)
                    return new Resposta(null, BotNaoConfigurado);
            }
            else if (http.BaseAddress is null)
            {
                return new Resposta(null, BotNaoConfigurado);
            }

            using var pedido = new HttpRequestMessage(metodo, urlRelativa);
            if (token is not null)
                pedido.Headers.Add("PRIVATE-TOKEN", token);
            if (conteudo is not null)
                pedido.Content = conteudo;

            try
            {
                return new Resposta(await http.SendAsync(pedido, cancellationToken), null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Só o tipo da exceção: a mensagem pode carregar detalhe do pedido (e o token de trigger vai no corpo).
                log.LogWarning("Falha ao chamar o GitLab para o grupo {Grupo} ({Tipo}).", grupoCaminho, ex.GetType().Name);
                return new Resposta(null, Indisponivel);
            }
        }

        // Corpo JSON com nomes snake_case explícitos (o GitLab não usa camelCase).
        private static HttpContent CorpoJson(object valor)
            => new StringContent(JsonSerializer.Serialize(valor), Encoding.UTF8, "application/json");

        private async Task<Result<T>> LerAsync<T>(HttpResponseMessage mensagem, Func<Stream, Task<Result<T>>> ler, CancellationToken cancellationToken)
        {
            if (!mensagem.IsSuccessStatusCode)
                return Result<T>.Falha(ErroDe(mensagem.StatusCode));

            try
            {
                await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                return await ler(corpo);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                log.LogWarning("Resposta do GitLab fora do formato esperado ({Tipo}).", ex.GetType().Name);
                return Result<T>.Falha(Indisponivel);
            }
        }

        private Erro ErroDe(HttpStatusCode status)
        {
            log.LogWarning("O GitLab respondeu {Status}.", (int)status);
            return Indisponivel;
        }
    }
}
