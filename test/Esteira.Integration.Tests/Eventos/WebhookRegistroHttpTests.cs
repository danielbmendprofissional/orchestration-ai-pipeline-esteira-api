using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Eventos;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Esteira.Integration.Tests.Eventos
{
    // T1.4, critérios 7, 9 e 10 na borda: cadastro grava WebhookProjeto + mensagem, o consumer (RabbitMQ real) registra o
    // hook no GitLab falso, reexecutar só MOR (409 fora de falhou) e a partida enfileira os Projetos sem linha.
    // Ambiente de mensageria próprio por host: filas separadas, sem outro host de teste consumindo a mensagem.
    [Trait("Fatia", "T1")]
    public sealed class WebhookRegistroHttpTests : IClassFixture<ApiDeTeste>
    {
        private const string Segredo = "segredo-do-webhook-reg-77";
        private const string UrlPublica = "https://esteira.teste";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly ApiDeTeste _api;
        private readonly InfraestruturaFixture _infra;

        public WebhookRegistroHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
        }

        private WebApplicationFactory<Program> Com(IGitLab gitlab, IGitLabHooks hooks, bool partida = false) => _api.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Webhook:Partida:Ligada"] = partida ? "true" : "false" }));
            b.UseSetting("Features:A1", "true");
            b.UseSetting("GitLab:Webhook:Secret", Segredo);
            b.UseSetting("Webhook:UrlPublica", UrlPublica);
            b.UseSetting("RabbitMQ:Ambiente", $"w{Guid.NewGuid():N}"[..12]);
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IGitLab>();
                s.AddSingleton(gitlab);
                s.RemoveAll<IGitLabHooks>();
                s.AddSingleton(hooks);
            });
        });

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static Task<HttpResponseMessage> EnviarAsync(
            WebApplicationFactory<Program> api, HttpMethod metodo, string url, string sessao, object? corpo = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            if (corpo is not null)
                pedido.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
            return cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private sealed record Cenario(long AppId, Guid AppPublicId, long GrupoId);

        private async Task<Cenario> AplicacaoAsync(string subAnalista)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupoId = ContextoDoCadastro.IdUnico();
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), grupoId, "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(PapelAplicacaoEntity.Criar(app.Id, subAnalista, PapelAplicacao.Analista));
            await ctx.SaveChangesAsync(Ct);
            return new Cenario(app.Id, app.PublicId, grupoId);
        }

        private static IGitLab GitLabCom(Cenario app, long projetoGitLabId)
        {
            var gitlab = Substitute.For<IGitLab>();
            gitlab.ListarProjetosDoGrupoAsync(Arg.Any<string>(), app.GrupoId, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso([new ProjetoGitLabDto(projetoGitLabId, "grupo/api", "api", "main")]));
            return gitlab;
        }

        private static IGitLabHooks HooksFalso(Result<long>? criar = null)
        {
            var hooks = Substitute.For<IGitLabHooks>();
            hooks.ListarAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<HookGitLabDto>>.Sucesso([]));
            hooks.CriarAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<HookParaRegistrar>(), Arg.Any<CancellationToken>())
                .Returns(criar ?? Result<long>.Sucesso(555));
            return hooks;
        }

        private async Task<WebhookProjetoEntity?> AguardarEstadoAsync(long projetoId, EstadoWebhookProjeto estado)
        {
            var limite = DateTime.UtcNow.AddSeconds(45);
            WebhookProjetoEntity? atual = null;
            while (DateTime.UtcNow < limite)
            {
                await using var ctx = ContextoDoCadastro.Novo(_infra);
                atual = await ctx.Set<WebhookProjetoEntity>().AsNoTracking().FirstOrDefaultAsync(w => w.ProjetoId == projetoId, Ct);
                if (atual?.Estado == estado)
                    return atual;
                await Task.Delay(200, Ct);
            }

            return atual;
        }

        private async Task<(Guid PublicId, long Id)> CadastrarAsync(WebApplicationFactory<Program> api, Cenario app, string sub, long gitlabId)
        {
            var resposta = await EnviarAsync(api, HttpMethod.Post, $"/api/v1/aplicacoes/{app.AppPublicId}/projetos",
                await SessaoAsync(api, sub), new { projetoGitLabId = gitlabId, stack = "dotnet" });
            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            var corpo = await CorpoAsync(resposta);
            // Resposta sai antes do commit: o estado do webhook já vai no DTO, ainda pendente.
            corpo.GetProperty("webhook").GetProperty("estado").GetString().Should().Be("pendente");
            var publicId = corpo.GetProperty("id").GetGuid();
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var id = await ctx.Set<ProjetoEntity>().Where(p => p.PublicId == publicId).Select(p => p.Id).SingleAsync(Ct);
            return (publicId, id);
        }

        [Fact, DisplayName("Critério 7: cadastrar grava webhook pendente + mensagem; o consumer cria o hook (URL pública, segredo) e passa a registrado")]
        public async Task CadastroRegistra()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync(sub);
            var gitlabId = ContextoDoCadastro.IdUnico();
            var hooks = HooksFalso();
            using var api = Com(GitLabCom(app, gitlabId), hooks);

            var (_, projetoId) = await CadastrarAsync(api, app, sub, gitlabId);

            var webhook = await AguardarEstadoAsync(projetoId, EstadoWebhookProjeto.Registrado);
            webhook!.Estado.Should().Be(EstadoWebhookProjeto.Registrado);
            webhook.GitLabHookId.Should().Be(555);
            await hooks.Received(1).CriarAsync("grupo", gitlabId,
                Arg.Is<HookParaRegistrar>(h => h.Url == $"{UrlPublica}/api/webhooks/gitlab" && h.Segredo == Segredo), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Critério 9: GitLab recusa -> cadastro segue 201 e webhook falhou com código; só MOR reexecuta; 409 fora de falhou")]
        public async Task FalhaEReexecuta()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync(sub);
            var gitlabId = ContextoDoCadastro.IdUnico();
            var hooks = HooksFalso(Result<long>.Falha(new Erro("gitlab.sem-permissao", "x", TipoErro.Forbidden)));
            using var api = Com(GitLabCom(app, gitlabId), hooks);
            var (publicId, projetoId) = await CadastrarAsync(api, app, sub, gitlabId);

            var falhou = await AguardarEstadoAsync(projetoId, EstadoWebhookProjeto.Falhou);
            falhou!.CodigoErro.Should().Be("webhook.sem-permissao");

            var url = $"/api/v1/projetos/{publicId}/webhook/reexecutar";
            var analista = await EnviarAsync(api, HttpMethod.Post, url, await SessaoAsync(api, sub));
            analista.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            hooks.CriarAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<HookParaRegistrar>(), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Sucesso(777));
            var mor = await EnviarAsync(api, HttpMethod.Post, url, await SessaoAsync(api, "sub-mor-wh", "mor"));
            mor.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(mor)).GetProperty("estado").GetString().Should().Be("pendente");

            var registrado = await AguardarEstadoAsync(projetoId, EstadoWebhookProjeto.Registrado);
            registrado!.GitLabHookId.Should().Be(777);

            var de_novo = await EnviarAsync(api, HttpMethod.Post, url, await SessaoAsync(api, "sub-mor-wh", "mor"));
            de_novo.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CorpoAsync(de_novo)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("webhook.nao-reexecutavel");
        }

        [Fact, DisplayName("Critério 10: ao subir, a API enfileira o registro dos Projetos ativos sem WebhookProjeto, uma vez só")]
        public async Task Partida()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync(sub);
            long projetoId;
            await using (var ctx = ContextoDoCadastro.Novo(_infra))
            {
                var gitlabId = ContextoDoCadastro.IdUnico();
                var projeto = ProjetoEntity.Criar(app.AppId, gitlabId, $"grupo/p{gitlabId}", $"p{gitlabId}", Stack.Dotnet);
                ctx.Add(projeto);
                await ctx.SaveChangesAsync(Ct);
                projetoId = projeto.Id;
            }

            var hooks = HooksFalso();
            using var api = Com(Substitute.For<IGitLab>(), hooks, partida: true);
            _ = api.Services;

            var webhook = await AguardarEstadoAsync(projetoId, EstadoWebhookProjeto.Registrado);
            webhook!.Estado.Should().Be(EstadoWebhookProjeto.Registrado);

            await using var leitura = ContextoDoCadastro.Novo(_infra);
            (await leitura.Set<WebhookProjetoEntity>().CountAsync(w => w.ProjetoId == projetoId, Ct)).Should().Be(1);
        }
    }
}
