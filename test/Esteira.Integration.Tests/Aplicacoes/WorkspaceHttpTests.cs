using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // P1.2 na borda HTTP: PUT /aplicacoes/{id}/workspace, sob Features:P1, com sessão simulada e IGitLab falso.
    [Trait("Fatia", "P1")]
    public sealed class WorkspaceHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class GitLabFalso : IGitLab
        {
            public Dictionary<long, ProjetoDoGrupoGitLabDto> Projetos { get; } = [];

            public Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<ProjetoDoGrupoGitLabDto?>.Sucesso(Projetos.GetValueOrDefault(projetoId)));

            public Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private readonly InfraestruturaFixture _infra;
        private readonly GitLabFalso _gitlab = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public WorkspaceHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string? p1) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:P1", p1 ?? string.Empty);
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IGitLab>();
                s.AddSingleton<IGitLab>(_gitlab);
            });
        });

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static Task<HttpResponseMessage> EnviarAsync(WebApplicationFactory<Program> api, HttpMethod metodo, string url, string sessao, object? corpo = null)
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

        private static async Task<string> CodigoDoErroAsync(HttpResponseMessage resposta)
            => (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString()!;

        private async Task<(Guid PublicId, long Id)> AplicacaoAsync(params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                ctx.Add(PoliticaSeveridadeEntity.Padrao(app.Id, amb));
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            return (app.PublicId, app.Id);
        }

        private long NovoProjetoGitLab(bool doGrupo = true)
        {
            var id = ContextoDoCadastro.IdUnico();
            _gitlab.Projetos[id] = new ProjetoDoGrupoGitLabDto(id, $"g/ws-{id}", $"ws-{id}", "main", doGrupo);
            return id;
        }

        [Fact, DisplayName("Critério 1: analista define o workspace (200), o detalhe devolve id e caminho e a auditoria guarda antes e depois")]
        public async Task Analista_Define()
        {
            var sub = $"sub-ws-{Guid.NewGuid():N}";
            var (id, _) = await AplicacaoAsync((sub, PapelAplicacao.Analista));
            var gl = NovoProjetoGitLab();
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = gl });

            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var detalhe = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{id}", sessao));
            var ws = detalhe.GetProperty("workspace");
            (ws.GetProperty("projetoGitLabId").GetInt64(), ws.GetProperty("caminho").GetString()).Should().Be((gl, $"g/ws-{gl}"));

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var auditoria = (await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == id).ToListAsync(Ct))
                .Should().ContainSingle().Subject;
            (auditoria.Usuario, auditoria.Acao, auditoria.Entidade).Should().Be((sub, AcaoAuditoria.Alterou, "Aplicacao"));
            auditoria.Antes.Should().NotBeNull();
            auditoria.Depois.Should().Contain($"g/ws-{gl}");
        }

        [Fact, DisplayName("Critério 1: null limpa o workspace e o detalhe volta com workspace null")]
        public async Task Mor_Limpa()
        {
            var (id, _) = await AplicacaoAsync();
            var gl = NovoProjetoGitLab();
            var sessao = await SessaoAsync(_ligada, "sub-mor-ws", "mor");
            (await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = gl })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = (long?)null });

            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var detalhe = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{id}", sessao));
            // A API omite campos nulos (serialização global): sem workspace, a propriedade não vem.
            detalhe.TryGetProperty("workspace", out var ws).Should().BeFalse();
        }

        [Fact, DisplayName("Critério 2: projeto fora do grupo responde 422 aplicacao.workspace-fora-do-grupo")]
        public async Task ForaDoGrupo()
        {
            var (id, _) = await AplicacaoAsync();
            var gl = NovoProjetoGitLab(doGrupo: false);
            var sessao = await SessaoAsync(_ligada, "sub-mor-ws-fora", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = gl });

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.workspace-fora-do-grupo");
        }

        [Fact, DisplayName("Critério 2: projeto já cadastrado como Projeto responde 409 aplicacao.workspace-e-projeto")]
        public async Task EProjeto()
        {
            var (id, appId) = await AplicacaoAsync();
            var gl = NovoProjetoGitLab();
            await using (var ctx = ContextoDoCadastro.Novo(_infra))
            {
                ctx.Add(ProjetoEntity.Criar(appId, gl, $"g/ws-{gl}", $"ws-{gl}", Stack.Dotnet));
                await ctx.SaveChangesAsync(Ct);
            }
            var sessao = await SessaoAsync(_ligada, "sub-mor-ws-proj", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = gl });

            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.workspace-e-projeto");
        }

        [Fact, DisplayName("Projeto inexistente responde 400 aplicacao.workspace-nao-encontrado")]
        public async Task Inexistente()
        {
            var (id, _) = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-ws-inex", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = 123456789L });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.workspace-nao-encontrado");
        }

        [Fact, DisplayName("Sem papel na Aplicação e sem MOR: 404 aplicacao.nao-encontrada; membro sem analista: 403")]
        public async Task Autorizacao()
        {
            var membro = $"sub-ws-membro-{Guid.NewGuid():N}";
            var (id, _) = await AplicacaoAsync((membro, PapelAplicacao.Membro));
            var gl = NovoProjetoGitLab();

            var semPapel = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace",
                await SessaoAsync(_ligada, "sub-ws-estranho"), new { projetoGitLabId = gl });
            var comoMembro = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace",
                await SessaoAsync(_ligada, membro), new { projetoGitLabId = gl });

            semPapel.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(semPapel)).Should().Be("aplicacao.nao-encontrada");
            comoMembro.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact, DisplayName("Critério 18: Features:P1 ausente responde 404 na rota nova; GET da A1 segue respondendo")]
        public async Task FlagDesligada()
        {
            var (id, _) = await AplicacaoAsync();
            var gl = NovoProjetoGitLab();
            var sessao = await SessaoAsync(_desligada, "sub-mor-ws-flag", "mor");

            var put = await EnviarAsync(_desligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/workspace", sessao, new { projetoGitLabId = gl });
            var get = await EnviarAsync(_desligada, HttpMethod.Get, $"/api/v1/aplicacoes/{id}", sessao);

            put.StatusCode.Should().Be(HttpStatusCode.NotFound);
            get.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
