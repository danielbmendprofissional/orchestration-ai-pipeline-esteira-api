using System.Net;
using System.Text;
using System.Text.Json;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Resultados;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esteira.AcL.GitLab
{
    // Adaptador da porta IGitLabHooks sobre a REST v4 (T1.4). O token do bot vai só no header PRIVATE-TOKEN; o segredo do
    // webhook, só no corpo do POST/PUT. Nenhum dos dois entra em erro, retorno ou log.
    public sealed class GitLabHooksHttp(HttpClient http, IOptions<GitLabOptions> opcoes, ILogger<GitLabHooksHttp> log) : IGitLabHooks
    {
        private const int PorPagina = 100;
        private const int MaximoDePaginas = 50;

        private static readonly Erro BotNaoConfigurado = new(
            "gitlab.bot-nao-configurado", "O bot do GitLab não está configurado para este grupo.", TipoErro.Failure);

        private static readonly Erro SemPermissao = new(
            "gitlab.sem-permissao", "O bot do GitLab não tem permissão para esta operação.", TipoErro.Forbidden);

        private static readonly Erro Indisponivel = new(
            "gitlab.indisponivel", "Não foi possível consultar o GitLab agora. Tente novamente.", TipoErro.Failure);

        public async Task<Result<IReadOnlyList<HookGitLabDto>>> ListarAsync(
            string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
        {
            var hooks = new List<HookGitLabDto>();
            var pagina = 1;

            while (pagina <= MaximoDePaginas)
            {
                var url = $"api/v4/projects/{projetoId}/hooks?per_page={PorPagina}&page={pagina}";
                var (mensagem, erro) = await EnviarAsync(grupoCaminho, HttpMethod.Get, url, null, cancellationToken);
                if (erro is not null)
                    return Result<IReadOnlyList<HookGitLabDto>>.Falha(erro);

                using (mensagem)
                {
                    if (!mensagem!.IsSuccessStatusCode)
                        return Result<IReadOnlyList<HookGitLabDto>>.Falha(ErroDe(mensagem.StatusCode));

                    try
                    {
                        await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                        using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                        // Só id e url: o resto da resposta (inclusive qualquer token) é descartado.
                        foreach (var item in doc.RootElement.EnumerateArray())
                            hooks.Add(new HookGitLabDto(item.GetProperty("id").GetInt64(), item.GetProperty("url").GetString() ?? string.Empty));
                    }
                    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                    {
                        log.LogWarning("Resposta do GitLab fora do formato esperado ao listar hooks ({Tipo}).", ex.GetType().Name);
                        return Result<IReadOnlyList<HookGitLabDto>>.Falha(Indisponivel);
                    }

                    var proxima = mensagem.Headers.TryGetValues("X-Next-Page", out var valores) ? valores.FirstOrDefault() : null;
                    if (!int.TryParse(proxima, out var numero) || numero <= pagina)
                        break;
                    pagina = numero;
                }
            }

            return Result<IReadOnlyList<HookGitLabDto>>.Sucesso(hooks);
        }

        public async Task<Result<long>> CriarAsync(
            string grupoCaminho, long projetoId, HookParaRegistrar hook, CancellationToken cancellationToken = default)
        {
            var (mensagem, erro) = await EnviarAsync(
                grupoCaminho, HttpMethod.Post, $"api/v4/projects/{projetoId}/hooks", Corpo(hook), cancellationToken);
            if (erro is not null)
                return Result<long>.Falha(erro);

            using (mensagem)
            {
                if (!mensagem!.IsSuccessStatusCode)
                    return Result<long>.Falha(ErroDe(mensagem.StatusCode));

                try
                {
                    await using var corpo = await mensagem.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(corpo, cancellationToken: cancellationToken);
                    return Result<long>.Sucesso(doc.RootElement.GetProperty("id").GetInt64());
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    log.LogWarning("Resposta do GitLab fora do formato esperado ao criar hook ({Tipo}).", ex.GetType().Name);
                    return Result<long>.Falha(Indisponivel);
                }
            }
        }

        public async Task<Result> AtualizarAsync(
            string grupoCaminho, long projetoId, long hookId, HookParaRegistrar hook, CancellationToken cancellationToken = default)
        {
            var (mensagem, erro) = await EnviarAsync(
                grupoCaminho, HttpMethod.Put, $"api/v4/projects/{projetoId}/hooks/{hookId}", Corpo(hook), cancellationToken);
            if (erro is not null)
                return Result.Falha(erro);

            using (mensagem)
                return mensagem!.IsSuccessStatusCode ? Result.Sucesso() : Result.Falha(ErroDe(mensagem.StatusCode));
        }

        // Eventos fixos: pipeline e merge request. Push desligado; SSL verificado.
        private static HttpContent Corpo(HookParaRegistrar hook)
            => new StringContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["url"] = hook.Url,
                ["token"] = hook.Segredo,
                ["push_events"] = false,
                ["pipeline_events"] = true,
                ["merge_requests_events"] = true,
                ["enable_ssl_verification"] = true
            }), Encoding.UTF8, "application/json");

        private async Task<(HttpResponseMessage? Mensagem, Erro? Erro)> EnviarAsync(
            string grupoCaminho, HttpMethod metodo, string urlRelativa, HttpContent? conteudo, CancellationToken cancellationToken)
        {
            var token = opcoes.Value.Bots.TryGetValue(grupoCaminho, out var bot) ? bot.Token : null;
            if (string.IsNullOrWhiteSpace(token) || http.BaseAddress is null)
                return (null, BotNaoConfigurado);

            using var pedido = new HttpRequestMessage(metodo, urlRelativa);
            pedido.Headers.Add("PRIVATE-TOKEN", token);
            if (conteudo is not null)
                pedido.Content = conteudo;

            try
            {
                return (await http.SendAsync(pedido, cancellationToken), null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Só o tipo da exceção: a mensagem pode carregar detalhe do pedido.
                log.LogWarning("Falha ao chamar o GitLab (hooks) para o grupo {Grupo} ({Tipo}).", grupoCaminho, ex.GetType().Name);
                return (null, Indisponivel);
            }
        }

        private Erro ErroDe(HttpStatusCode status)
        {
            log.LogWarning("O GitLab respondeu {Status} nos hooks.", (int)status);
            return status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? SemPermissao : Indisponivel;
        }
    }
}
