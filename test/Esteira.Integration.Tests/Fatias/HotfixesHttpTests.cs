using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Api.Controllers;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Fatias
{
    // P1.7 na borda HTTP: GET /aplicacoes/{id}/hotfixes sob Features:P1, com sessão simulada e IGitLab falso (só leitura).
    [Trait("Fatia", "P1")]
    public sealed class HotfixesHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class GitLabFalso : IGitLab
        {
            public Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<IReadOnlyList<IssueGitLabDto>>.Sucesso([
                    new IssueGitLabDto(7, "Hotfix: corrige o cálculo", "opened", ["ready-for-human"]),
                    new IssueGitLabDto(8, "Outra coisa", "opened", ["ready-for-human"])]));

            public Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<IReadOnlyList<MergeRequestGitLabDto>>.Sucesso([]));

            public Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public HotfixesHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private static WebApplicationFactory<Program> Com(ApiDeTeste api, string? p1) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:P1", p1 ?? string.Empty);
            b.UseSetting("GitLab:Url", "https://gitlab.exemplo.com");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IGitLab>();
                s.AddSingleton<IGitLab>(new GitLabFalso());
            });
        });

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, []);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static Task<HttpResponseMessage> GetAsync(WebApplicationFactory<Program> api, string url, string sessao)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            return cliente.SendAsync(pedido, Ct);
        }

        private async Task<Guid> AplicacaoAsync(string nome, string sub)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupo = $"g{Guid.NewGuid():N}"[..10];
            var app = AplicacaoEntity.Criar(nome, ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), grupo).Value;
            app.DefinirWorkspace(ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-workspace");
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-api", $"{nome}-api", Stack.Dotnet));
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                ctx.Add(PoliticaSeveridadeEntity.Padrao(app.Id, amb));
            ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        [Fact, DisplayName("Critério 18: Features:P1 ausente responde 404 em /hotfixes")]
        public async Task FlagDesligada()
        {
            var sub = $"sub-h-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("hflag", sub);

            var resposta = await GetAsync(_desligada, $"/api/v1/aplicacoes/{app}/hotfixes", await SessaoAsync(_desligada, sub));

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Sem papel na Aplicação responde 404 aplicacao.nao-encontrada")]
        public async Task SemPapel()
        {
            var app = await AplicacaoAsync("hsempapel", $"sub-h-{Guid.NewGuid():N}");

            var resposta = await GetAsync(_ligada, $"/api/v1/aplicacoes/{app}/hotfixes", await SessaoAsync(_ligada, "sub-estranho-h"));

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("aplicacao.nao-encontrada");
        }

        [Fact, DisplayName("Com papel responde 200 com aplicacaoId, itens e aviso; só a issue Hotfix com ready-for-human entra")]
        public async Task ComPapel()
        {
            var sub = $"sub-h-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("hok", sub);

            var resposta = await GetAsync(_ligada, $"/api/v1/aplicacoes/{app}/hotfixes", await SessaoAsync(_ligada, sub));

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            var corpo = doc.RootElement;
            corpo.GetProperty("aplicacaoId").GetGuid().Should().Be(app);
            // A API omite campos nulos: sem aviso, a propriedade ausente (ou null) significa "sem aviso".
            (!corpo.TryGetProperty("aviso", out var aviso) || aviso.ValueKind == JsonValueKind.Null).Should().BeTrue();
            var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
            itens.Should().ContainSingle();
            itens[0].GetProperty("issue").GetInt64().Should().Be(7);
            itens[0].GetProperty("motivo").GetString().Should().Be("corrige o cálculo");
            itens[0].GetProperty("estado").GetString().Should().Be("aguardando-a-correcao");
        }

        [Fact, DisplayName("Sem sessão /hotfixes responde 401")]
        public async Task SemSessao()
        {
            var cliente = _ligada.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            (await cliente.GetAsync($"/api/v1/aplicacoes/{Guid.NewGuid()}/hotfixes", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    // P1 é só leitura no GitLab: toda rota do FatiasController é GET, salvo POST .../fatias/atualizar (renova o cache, não escreve no GitLab).
    [Trait("Fatia", "P1")]
    public sealed class RotasDeP1SoLeituraTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
    {
        [Fact, DisplayName("Todas as rotas do FatiasController são GET, exceto POST fatias/atualizar")]
        public void ControllerSoExpoeGet()
        {
            // As três rotas de decisão da P2 (atrás de Features:P2) só registram a Ação do bot; a prova de que a escrita no GitLab não existe é a P2.
            var rotas = typeof(FatiasController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<Esteira.Api.Features.FeatureFlagAttribute>()?.Fatia != "P2")
                .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(true))
                .ToList();

            rotas.Should().NotBeEmpty();
            rotas.Where(r => !r.HttpMethods.SequenceEqual(["GET"])).Select(r => $"{r.HttpMethods.Single()} {r.Template}")
                .Should().BeEquivalentTo(["POST aplicacoes/{id:guid}/fatias/atualizar"]);
            rotas.Where(r => r.HttpMethods.SequenceEqual(["GET"])).Select(r => r.Template)
                .Should().BeEquivalentTo(["fatias", "aplicacoes/{id:guid}/hotfixes", "aplicacoes/{id:guid}/fatias/{codigo}"]);
        }

        [Fact, DisplayName("O OpenAPI descreve as rotas de P1 só com GET, salvo POST fatias/atualizar")]
        public async Task OpenApiSoExpoeGet()
        {
            var doc = JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json", Ct))!;
            var paths = doc["paths"]!.AsObject();

            Metodos(paths, "/api/v1/fatias").Should().BeEquivalentTo(["get"]);
            Metodos(paths, "/api/v1/aplicacoes/{id}/hotfixes").Should().BeEquivalentTo(["get"]);
            Metodos(paths, "/api/v1/aplicacoes/{id}/fatias/{codigo}").Should().BeEquivalentTo(["get"]);
            Metodos(paths, "/api/v1/aplicacoes/{id}/fatias/atualizar").Should().BeEquivalentTo(["post"]);
        }

        private static string[] Metodos(JsonObject paths, string rota)
            => paths[rota]!.AsObject().Select(p => p.Key).ToArray();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;
    }
}
