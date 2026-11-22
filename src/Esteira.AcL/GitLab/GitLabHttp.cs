using System.Net;
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

    // Adaptador da porta IGitLab sobre a REST v4. Só leitura (GET). O token vai só no header PRIVATE-TOKEN de cada
    // pedido e nunca entra em mensagem de erro nem em log.
    public sealed class GitLabHttp(HttpClient http, IOptions<GitLabOptions> opcoes, ILogger<GitLabHttp> log) : IGitLab
    {
        private const int PorPagina = 100;
        private const int MaximoDePaginas = 50;

        private static readonly Erro BotNaoConfigurado = new(
            "gitlab.bot-nao-configurado", "O bot do GitLab não está configurado para este grupo.", TipoErro.Failure);

        private static readonly Erro GrupoNaoEncontrado = new(
            "gitlab.grupo-nao-encontrado", "Grupo não encontrado no GitLab, ou o bot não tem acesso a ele.", TipoErro.Failure);

        private static readonly Erro Indisponivel = new(
            "gitlab.indisponivel", "Não foi possível consultar o GitLab agora. Tente novamente.", TipoErro.Failure);

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

        private readonly record struct Resposta(HttpResponseMessage? Mensagem, Erro? Erro);

        // Resolve o token do grupo e faz o GET. Devolve o Erro pronto quando não dá para chamar ou a chamada falha.
        private async Task<Resposta> EnviarAsync(string grupoCaminho, string urlRelativa, CancellationToken cancellationToken)
        {
            var token = opcoes.Value.Bots.TryGetValue(grupoCaminho, out var bot) ? bot.Token : null;
            if (string.IsNullOrWhiteSpace(token) || http.BaseAddress is null)
                return new Resposta(null, BotNaoConfigurado);

            using var pedido = new HttpRequestMessage(HttpMethod.Get, urlRelativa);
            pedido.Headers.Add("PRIVATE-TOKEN", token);

            try
            {
                var resposta = await http.SendAsync(pedido, cancellationToken);
                if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                {
                    resposta.Dispose();
                    log.LogWarning("O GitLab recusou o token do bot do grupo {Grupo}.", grupoCaminho);
                    return new Resposta(null, BotNaoConfigurado);
                }

                return new Resposta(resposta, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Só o tipo da exceção: a mensagem pode carregar detalhe do pedido.
                log.LogWarning("Falha ao chamar o GitLab para o grupo {Grupo} ({Tipo}).", grupoCaminho, ex.GetType().Name);
                return new Resposta(null, Indisponivel);
            }
        }

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
