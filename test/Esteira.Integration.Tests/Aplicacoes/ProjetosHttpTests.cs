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
    // A1.4 na borda HTTP: projetos do GitLab, cadastro de Projeto, dependências, Ambientes e Destinos, com sessão simulada,
    // IGitLab falso, papéis (analista/MOR escrevem; 404 sem papel; 403 papel insuficiente), flag e auditoria na mesma transação.
    [Trait("Fatia", "A1")]
    public sealed class ProjetosHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class GitLabFalso : IGitLab
        {
            public Task<Result<MergeRequestDaAcaoGitLabDto>> ObterMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<string>>> ListarNotasMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<long>> ComentarMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, string corpo, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<MergeRequestDaAcaoGitLabDto>> FecharMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<MergeRequestDaAcaoGitLabDto>> ReabrirMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<MergeRequestDaAcaoGitLabDto>> MergearMergeRequestAsync( string grupoCaminho, long projetoId, long mrIid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<TriggerTokenGitLabDto>> CriarTriggerTokenAsync( string grupoCaminho, long projetoId, string descricao, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<TriggerTokenListadoGitLabDto>>> ListarTriggerTokensAsync( string grupoCaminho, long projetoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result> ApagarTriggerTokenAsync( string grupoCaminho, long projetoId, long triggerTokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<long>> CriarPipelinePorTriggerAsync( string grupoCaminho, long projetoId, string triggerToken, string referencia, IReadOnlyDictionary<string, string> variaveis, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<PipelineGitLabDto>> ObterPipelineAsync( string grupoCaminho, long projetoId, long pipelineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<Result<PipelineGitLabDto>> CancelarPipelineAsync( string grupoCaminho, long projetoId, long pipelineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Dictionary<long, List<ProjetoGitLabDto>> PorGrupo { get; } = new();
            public Dictionary<long, string> Arquivos { get; } = new();

            public Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso(PorGrupo.TryGetValue(grupoId, out var l) ? l : []));

            public Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<string?>.Sucesso(Arquivos.TryGetValue(projetoId, out var c) ? c : null));

            public Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private sealed record Aplic(Guid PublicId, long Id, long GrupoId);

        private readonly InfraestruturaFixture _infra;
        private readonly GitLabFalso _gitlab = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public ProjetosHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, "false");
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string flag) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", flag);
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

        private static Task<HttpResponseMessage> EnviarAsync(
            WebApplicationFactory<Program> api, HttpMethod metodo, string url, string? sessao, object? corpo = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            if (corpo is not null)
                pedido.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
            return cliente.SendAsync(pedido, Ct);
        }

        private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string url, string? sessao, object? corpo = null)
            => EnviarAsync(_ligada, metodo, url, sessao, corpo);

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private static async Task<string> CodigoDoErroAsync(HttpResponseMessage resposta)
            => (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString()!;

        private async Task<Aplic> AplicacaoAsync(params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupoId = ContextoDoCadastro.IdUnico();
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), grupoId, "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            _gitlab.PorGrupo[grupoId] = [];
            return new Aplic(app.PublicId, app.Id, grupoId);
        }

        private async Task<Aplic> AplicacaoComAnalistaAsync(string sub) => await AplicacaoAsync((sub, PapelAplicacao.Analista));

        // Projeto já cadastrado (com os 3 Ambientes), direto no banco.
        private async Task<ProjetoEntity> ProjetoAsync(Aplic app, long? gitlabId = null)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var id = gitlabId ?? ContextoDoCadastro.IdUnico();
            var projeto = ProjetoEntity.Criar(app.Id, id, $"grupo/p{id}", $"p{id}", Stack.Dotnet);
            ctx.Add(projeto);
            await ctx.SaveChangesAsync(Ct);
            foreach (var amb in Enum.GetValues<AmbienteDeploy>())
                ctx.Add(AmbienteProjetoEntity.Criar(projeto.Id, amb).Value);
            await ctx.SaveChangesAsync(Ct);
            return projeto;
        }

        private async Task<List<RegistroAuditoriaEntity>> AuditoriaAsync(Guid publicId)
        {
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            return await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == publicId).ToListAsync(Ct);
        }

        // ---- GET /aplicacoes/{id}/gitlab/projetos (critério 5) ----

        [Fact, DisplayName("Critério 5: analista lista os projetos do grupo com cadastrados marcados e stack sugerida")]
        public async Task ListaProjetosDoGrupo()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var cadastrado = await ProjetoAsync(app);
            long livre = ContextoDoCadastro.IdUnico();
            _gitlab.PorGrupo[app.GrupoId] =
            [
                new ProjetoGitLabDto(cadastrado.ProjetoGitLabId, cadastrado.Caminho, cadastrado.Nome, "main"),
                new ProjetoGitLabDto(livre, "grupo/livre", "livre", "main")
            ];
            _gitlab.Arquivos[livre] = "variables:\n  APP_TECHNOLOGY: angular\n";
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/gitlab/projetos", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var itens = (await CorpoAsync(resposta)).EnumerateArray().ToList();
            var marcado = itens.Single(i => i.GetProperty("projetoGitLabId").GetInt64() == cadastrado.ProjetoGitLabId);
            marcado.GetProperty("cadastrado").GetBoolean().Should().BeTrue();
            // A API omite null (Newtonsoft com NullValueHandling.Ignore): sem sugestão = campo ausente.
            marcado.TryGetProperty("stackSugerida", out _).Should().BeFalse();
            var novo = itens.Single(i => i.GetProperty("projetoGitLabId").GetInt64() == livre);
            (novo.GetProperty("cadastrado").GetBoolean(), novo.GetProperty("stackSugerida").GetString(), novo.GetProperty("caminho").GetString(), novo.GetProperty("nome").GetString())
                .Should().Be((false, "angular", "grupo/livre", "livre"));
        }

        [Fact, DisplayName("Critério 5: marca como cadastrado o projeto que está em outra Aplicação")]
        public async Task ListaMarcaDeOutraAplicacao()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var outra = await AplicacaoAsync();
            var deOutra = await ProjetoAsync(outra);
            _gitlab.PorGrupo[app.GrupoId] = [new ProjetoGitLabDto(deOutra.ProjetoGitLabId, "x/y", "y", "main")];
            var sessao = await SessaoAsync(_ligada, sub);

            var itens = await CorpoAsync(await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/gitlab/projetos", sessao));

            itens[0].GetProperty("cadastrado").GetBoolean().Should().BeTrue();
        }

        [Fact, DisplayName("Critério 13: membro recebe 403 e sem papel 404 ao listar projetos do GitLab")]
        public async Task ListaSemPermissao()
        {
            var sub = $"sub-mem-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync((sub, PapelAplicacao.Membro));

            var r403 = await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/gitlab/projetos", await SessaoAsync(_ligada, sub));
            var r404 = await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/gitlab/projetos", await SessaoAsync(_ligada, "sub-estranho-gl"));

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // ---- POST /aplicacoes/{id}/projetos (critérios 6, 7, 8) ----

        [Fact, DisplayName("Critérios 6 e 8: analista cadastra o projeto do grupo (201), com qa, hml e producao habilitados e auditoria")]
        public async Task CadastraProjeto()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var gitlabId = ContextoDoCadastro.IdUnico();
            _gitlab.PorGrupo[app.GrupoId] = [new ProjetoGitLabDto(gitlabId, "grupo/api", "api", "main")];
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/projetos", sessao, new { projetoGitLabId = gitlabId, stack = "dotnet" });

            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            var corpo = await CorpoAsync(resposta);
            var id = corpo.GetProperty("id").GetGuid();
            resposta.Headers.Location!.ToString().Should().EndWith($"/api/v1/projetos/{id}");
            (corpo.GetProperty("nome").GetString(), corpo.GetProperty("caminho").GetString(), corpo.GetProperty("stack").GetString(), corpo.GetProperty("ativo").GetBoolean())
                .Should().Be(("api", "grupo/api", "dotnet", true));
            var ambientes = corpo.GetProperty("ambientes").EnumerateArray().ToList();
            ambientes.Select(a => a.GetProperty("ambiente").GetString()).Should().Equal("qa", "hml", "producao");
            ambientes.Should().OnlyContain(a => a.GetProperty("habilitado").GetBoolean() && a.GetProperty("destinos").GetArrayLength() == 0);
            corpo.GetProperty("dependeDe").GetArrayLength().Should().Be(0);

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var projeto = await ctx.Set<ProjetoEntity>().SingleAsync(p => p.PublicId == id, Ct);
            projeto.AplicacaoId.Should().Be(app.Id);
            (await ctx.Set<AmbienteProjetoEntity>().CountAsync(a => a.ProjetoId == projeto.Id && a.Habilitado, Ct)).Should().Be(3);
            var registro = (await AuditoriaAsync(id)).Should().ContainSingle().Subject;
            (registro.Usuario, registro.Acao, registro.Entidade).Should().Be((sub, AcaoAuditoria.Criou, "Projeto"));
            registro.Antes.Should().BeNull();
        }

        [Fact, DisplayName("Critério 6: projeto que não é do grupo responde 422 projeto.fora-do-grupo e não grava")]
        public async Task ForaDoGrupo()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var gitlabId = ContextoDoCadastro.IdUnico();
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/projetos", sessao, new { projetoGitLabId = gitlabId, stack = "dotnet" });

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("projeto.fora-do-grupo");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<ProjetoEntity>().AnyAsync(p => p.ProjetoGitLabId == gitlabId, Ct)).Should().BeFalse();
        }

        [Fact, DisplayName("Critério 7: projeto já cadastrado (mesmo em outra Aplicação) responde 409 projeto.duplicado, sem nova auditoria")]
        public async Task Duplicado()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var existente = await ProjetoAsync(await AplicacaoAsync());
            _gitlab.PorGrupo[app.GrupoId] = [new ProjetoGitLabDto(existente.ProjetoGitLabId, "x/y", "y", "main")];
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/projetos", sessao, new { projetoGitLabId = existente.ProjetoGitLabId, stack = "dotnet" });

            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(resposta)).Should().Be("projeto.duplicado");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<ProjetoEntity>().CountAsync(p => p.ProjetoGitLabId == existente.ProjetoGitLabId, Ct)).Should().Be(1);
        }

        [Theory, DisplayName("Validação do cadastro de projeto: 400 com code estável")]
        [InlineData("{\"stack\":\"dotnet\"}", "projeto.gitlab-id-obrigatorio")]
        [InlineData("{\"projetoGitLabId\":5,\"stack\":\"cobol\"}", "projeto.stack-invalida")]
        public async Task CadastroInvalido(string json, string codigo)
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var sessao = await SessaoAsync(_ligada, sub);
            var cliente = _ligada.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/projetos") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");

            var resposta = await cliente.SendAsync(pedido, Ct);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be(codigo);
        }

        [Fact, DisplayName("MOR cadastra projeto sem ter papel; membro recebe 403; sem papel 404")]
        public async Task CadastroAutorizacao()
        {
            var membro = $"sub-mem-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync((membro, PapelAplicacao.Membro));
            var gitlabId = ContextoDoCadastro.IdUnico();
            _gitlab.PorGrupo[app.GrupoId] = [new ProjetoGitLabDto(gitlabId, "grupo/api", "api")];
            var corpo = new { projetoGitLabId = gitlabId, stack = "outro" };
            var url = $"/api/v1/aplicacoes/{app.PublicId}/projetos";

            var r403 = await Enviar(HttpMethod.Post, url, await SessaoAsync(_ligada, membro), corpo);
            var r404 = await Enviar(HttpMethod.Post, url, await SessaoAsync(_ligada, "sub-estranho-cad"), corpo);
            var rMor = await Enviar(HttpMethod.Post, url, await SessaoAsync(_ligada, "sub-mor-cad", "mor"), corpo);

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
            rMor.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // ---- PUT /projetos/{id} ----

        [Fact, DisplayName("Analista altera stack e ativo do Projeto (200) e a auditoria guarda antes e depois")]
        public async Task AlteraProjeto()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}", sessao, new { stack = "angular", ativo = false });

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            (corpo.GetProperty("stack").GetString(), corpo.GetProperty("ativo").GetBoolean()).Should().Be(("angular", false));
            var registro = (await AuditoriaAsync(projeto.PublicId)).Should().ContainSingle().Subject;
            JsonDocument.Parse(registro.Antes!).RootElement.GetProperty("stack").GetString().Should().Be("dotnet");
            JsonDocument.Parse(registro.Depois!).RootElement.GetProperty("stack").GetString().Should().Be("angular");
        }

        [Fact, DisplayName("Projeto inexistente responde 404 projeto.nao-encontrado; ativo omitido responde 400")]
        public async Task AlteraProjetoErros()
        {
            var sessao = await SessaoAsync(_ligada, "sub-mor-pj", "mor");
            var app = await AplicacaoAsync();
            var projeto = await ProjetoAsync(app);

            var r404 = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{Guid.NewGuid()}", sessao, new { stack = "dotnet", ativo = true });
            var r400 = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}", sessao, new { stack = "dotnet" });

            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(r404)).Should().Be("projeto.nao-encontrado");
            r400.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(r400)).Should().Be("projeto.ativo-obrigatorio");
        }

        [Fact, DisplayName("Critério 13: membro altera Projeto = 403; sem papel = 404 (sem revelar o Projeto)")]
        public async Task AlteraProjetoSemPermissao()
        {
            var membro = $"sub-mem-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync((membro, PapelAplicacao.Membro));
            var projeto = await ProjetoAsync(app);
            var corpo = new { stack = "angular", ativo = true };

            var r403 = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}", await SessaoAsync(_ligada, membro), corpo);
            var r404 = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}", await SessaoAsync(_ligada, "sub-estranho-pj"), corpo);

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await AuditoriaAsync(projeto.PublicId)).Should().BeEmpty();
        }

        // ---- PUT /projetos/{id}/dependencias (critério 15) ----

        [Fact, DisplayName("Critério 15: substitui as dependências (200), devolve dependeDe e audita antes/depois")]
        public async Task Dependencias()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var a = await ProjetoAsync(app);
            var b = await ProjetoAsync(app);
            var c = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);

            var primeira = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", sessao, new { dependeDe = new[] { b.PublicId } });
            var segunda = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", sessao, new { dependeDe = new[] { c.PublicId } });

            primeira.StatusCode.Should().Be(HttpStatusCode.OK);
            segunda.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(segunda)).GetProperty("dependeDe").EnumerateArray().Select(x => x.GetGuid()).Should().Equal(c.PublicId);
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<DependenciaProjetoEntity>().Where(d => d.ProjetoId == a.Id).Select(d => d.DependeDeProjetoId).ToListAsync(Ct)).Should().Equal(c.Id);
            (await AuditoriaAsync(a.PublicId)).Should().HaveCount(2);
        }

        [Fact, DisplayName("Critério 15: lista vazia remove as dependências")]
        public async Task DependenciasVazia()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var a = await ProjetoAsync(app);
            var b = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);
            await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", sessao, new { dependeDe = new[] { b.PublicId } });

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", sessao, new { dependeDe = Array.Empty<Guid>() });

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(resposta)).GetProperty("dependeDe").GetArrayLength().Should().Be(0);
        }

        [Fact, DisplayName("Critério 15: outro Projeto de outra Aplicação, o próprio e ciclo respondem 400 projeto.dependencia-invalida e não gravam")]
        public async Task DependenciasInvalidas()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var a = await ProjetoAsync(app);
            var b = await ProjetoAsync(app);
            var deOutraApp = await ProjetoAsync(await AplicacaoAsync());
            var sessao = await SessaoAsync(_ligada, sub);
            // b depende de a: a passar a depender de b fecha ciclo.
            (await Enviar(HttpMethod.Put, $"/api/v1/projetos/{b.PublicId}/dependencias", sessao, new { dependeDe = new[] { a.PublicId } })).StatusCode.Should().Be(HttpStatusCode.OK);

            foreach (var alvo in new[] { deOutraApp.PublicId, a.PublicId, b.PublicId, Guid.NewGuid() })
            {
                var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", sessao, new { dependeDe = new[] { alvo } });

                resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                (await CodigoDoErroAsync(resposta)).Should().Be("projeto.dependencia-invalida");
            }

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<DependenciaProjetoEntity>().AnyAsync(d => d.ProjetoId == a.Id, Ct)).Should().BeFalse();
            (await AuditoriaAsync(a.PublicId)).Should().BeEmpty();
        }

        [Fact, DisplayName("dependeDe omitido responde 400 projeto.depende-de-obrigatorio")]
        public async Task DependenciasOmitida()
        {
            var app = await AplicacaoAsync();
            var a = await ProjetoAsync(app);

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{a.PublicId}/dependencias", await SessaoAsync(_ligada, "sub-mor-dep", "mor"), new { });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("projeto.depende-de-obrigatorio");
        }

        // ---- PUT /projetos/{id}/ambientes/{ambiente} (critério 9) ----

        [Fact, DisplayName("Desabilita e reabilita hml (200) e a auditoria guarda antes e depois")]
        public async Task AlteraAmbiente()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml", sessao, new { habilitado = false });

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var hml = (await CorpoAsync(resposta)).GetProperty("ambientes").EnumerateArray().Single(a => a.GetProperty("ambiente").GetString() == "hml");
            hml.GetProperty("habilitado").GetBoolean().Should().BeFalse();
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var linha = await ctx.Set<AmbienteProjetoEntity>().SingleAsync(a => a.ProjetoId == projeto.Id && a.Ambiente == AmbienteDeploy.Hml, Ct);
            linha.Habilitado.Should().BeFalse();
            var registro = (await AuditoriaAsync(linha.PublicId)).Should().ContainSingle().Subject;
            JsonDocument.Parse(registro.Antes!).RootElement.GetProperty("habilitado").GetBoolean().Should().BeTrue();
            JsonDocument.Parse(registro.Depois!).RootElement.GetProperty("habilitado").GetBoolean().Should().BeFalse();
        }

        [Fact, DisplayName("Critério 9: desabilitar producao responde 400 ambiente.producao-obrigatorio e não grava")]
        public async Task ProducaoObrigatorio()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}/ambientes/producao", await SessaoAsync(_ligada, sub), new { habilitado = false });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("ambiente.producao-obrigatorio");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<AmbienteProjetoEntity>().SingleAsync(a => a.ProjetoId == projeto.Id && a.Ambiente == AmbienteDeploy.Producao, Ct)).Habilitado.Should().BeTrue();
        }

        [Theory, DisplayName("Ambiente fora do vocabulário (dev, review) responde 400 ambiente.invalido")]
        [InlineData("dev")]
        [InlineData("review")]
        public async Task AmbienteInvalido(string ambiente)
        {
            var projeto = await ProjetoAsync(await AplicacaoAsync());

            var resposta = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}/ambientes/{ambiente}", await SessaoAsync(_ligada, "sub-mor-amb2", "mor"), new { habilitado = true });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("ambiente.invalido");
        }

        // ---- Destinos (critério 10) ----

        private static object Destino(string nome = "k8s-hml", Dictionary<string, string>? cfg = null, int ordem = 1)
            => new { nome, configuracao = cfg ?? new Dictionary<string, string> { ["KUBE_CONTEXT"] = "hml" }, ordem };

        [Fact, DisplayName("Cria, altera e remove Destino (201/200/204), com auditoria antes/depois e a configuração no detalhe da Aplicação")]
        public async Task CicloDeDestino()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);
            var baseUrl = $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos";

            var criado = await Enviar(HttpMethod.Post, baseUrl, sessao, Destino());
            criado.StatusCode.Should().Be(HttpStatusCode.Created);
            var corpo = await CorpoAsync(criado);
            var id = corpo.GetProperty("id").GetGuid();
            criado.Headers.Location!.ToString().Should().EndWith($"{baseUrl}/{id}");
            (corpo.GetProperty("nome").GetString(), corpo.GetProperty("ordem").GetInt32(), corpo.GetProperty("configuracao").GetProperty("KUBE_CONTEXT").GetString())
                .Should().Be(("k8s-hml", 1, "hml"));

            var detalhe = await CorpoAsync(await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}", sessao));
            var proj = detalhe.GetProperty("projetos").EnumerateArray().Single();
            var destinos = proj.GetProperty("ambientes").EnumerateArray().Single(a => a.GetProperty("ambiente").GetString() == "hml").GetProperty("destinos");
            destinos.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(id);
            (proj.GetProperty("stack").GetString(), proj.GetProperty("ativo").GetBoolean(), proj.GetProperty("ambientes").GetArrayLength()).Should().Be(("dotnet", true, 3));
            proj.GetProperty("dependeDe").GetArrayLength().Should().Be(0);

            var alterado = await Enviar(HttpMethod.Put, $"{baseUrl}/{id}", sessao, Destino("k8s-hml-2", new Dictionary<string, string> { ["NAMESPACE"] = "valuto" }, 3));
            alterado.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(alterado)).GetProperty("nome").GetString().Should().Be("k8s-hml-2");

            var removido = await Enviar(HttpMethod.Delete, $"{baseUrl}/{id}", sessao);
            removido.StatusCode.Should().Be(HttpStatusCode.NoContent);

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<DestinoEntity>().AnyAsync(d => d.PublicId == id, Ct)).Should().BeFalse();
            var trilha = (await AuditoriaAsync(id)).OrderBy(r => r.Id).ToList();
            trilha.Select(r => r.Acao).Should().Equal(AcaoAuditoria.Criou, AcaoAuditoria.Alterou, AcaoAuditoria.Removeu);
            JsonDocument.Parse(trilha[1].Antes!).RootElement.GetProperty("nome").GetString().Should().Be("k8s-hml");
            JsonDocument.Parse(trilha[1].Depois!).RootElement.GetProperty("nome").GetString().Should().Be("k8s-hml-2");
            trilha[2].Depois.Should().BeNull();
        }

        [Fact, DisplayName("Critério 10: chave de segredo responde 400 destino.configuracao-com-segredo, orienta a usar variável do GitLab, não grava nem audita e não vaza o valor")]
        public async Task DestinoComSegredo()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos", await SessaoAsync(_ligada, sub),
                Destino(cfg: new Dictionary<string, string> { ["db_password"] = "valor-super-secreto" }));

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var texto = await resposta.Content.ReadAsStringAsync(Ct);
            texto.Should().Contain("destino.configuracao-com-segredo").And.Contain("GitLab").And.NotContain("valor-super-secreto");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<DestinoEntity>().AnyAsync(d => d.ProjetoId == projeto.Id, Ct)).Should().BeFalse();
            (await ctx.Set<RegistroAuditoriaEntity>().AnyAsync(r => r.Usuario == sub && r.Entidade == "Destino", Ct)).Should().BeFalse();
        }

        [Fact, DisplayName("Nome de Destino repetido em Projeto+Ambiente responde 409 destino.nome-duplicado; em outro ambiente é aceito")]
        public async Task DestinoNomeDuplicado()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);
            var hml = $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos";
            (await Enviar(HttpMethod.Post, hml, sessao, Destino())).StatusCode.Should().Be(HttpStatusCode.Created);

            var repetido = await Enviar(HttpMethod.Post, hml, sessao, Destino());
            var outroAmbiente = await Enviar(HttpMethod.Post, $"/api/v1/projetos/{projeto.PublicId}/ambientes/qa/destinos", sessao, Destino());

            repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(repetido)).Should().Be("destino.nome-duplicado");
            outroAmbiente.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact, DisplayName("Ambiente desabilitado não aceita Destino: 422 ambiente.desabilitado")]
        public async Task DestinoEmAmbienteDesabilitado()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var app = await AplicacaoComAnalistaAsync(sub);
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, sub);
            await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}/ambientes/qa", sessao, new { habilitado = false });

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/projetos/{projeto.PublicId}/ambientes/qa/destinos", sessao, Destino());

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("ambiente.desabilitado");
        }

        [Fact, DisplayName("Destino de outro ambiente/Projeto ou inexistente responde 404 destino.nao-encontrado")]
        public async Task DestinoNaoEncontrado()
        {
            var app = await AplicacaoAsync();
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, "sub-mor-dest", "mor");
            var criado = await CorpoAsync(await Enviar(HttpMethod.Post, $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos", sessao, Destino()));
            var id = criado.GetProperty("id").GetGuid();

            var outroAmbiente = await Enviar(HttpMethod.Delete, $"/api/v1/projetos/{projeto.PublicId}/ambientes/qa/destinos/{id}", sessao);
            var inexistente = await Enviar(HttpMethod.Put, $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos/{Guid.NewGuid()}", sessao, Destino("x"));

            outroAmbiente.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(outroAmbiente)).Should().Be("destino.nao-encontrado");
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Critério 13: membro cria Destino = 403; sem papel = 404")]
        public async Task DestinoSemPermissao()
        {
            var membro = $"sub-mem-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync((membro, PapelAplicacao.Membro));
            var projeto = await ProjetoAsync(app);
            var url = $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos";

            var r403 = await Enviar(HttpMethod.Post, url, await SessaoAsync(_ligada, membro), Destino());
            var r404 = await Enviar(HttpMethod.Post, url, await SessaoAsync(_ligada, "sub-estranho-dest"), Destino());

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Remover o Projeto-pai da cascata não deixa Destino órfão: Destino some junto com o Projeto no banco")]
        public async Task CascataDoDestino()
        {
            var app = await AplicacaoAsync();
            var projeto = await ProjetoAsync(app);
            var sessao = await SessaoAsync(_ligada, "sub-mor-casc", "mor");
            await Enviar(HttpMethod.Post, $"/api/v1/projetos/{projeto.PublicId}/ambientes/hml/destinos", sessao, Destino());

            await using (var ctx = ContextoDoCadastro.Novo(_infra))
            {
                ctx.Remove(await ctx.Set<ProjetoEntity>().SingleAsync(p => p.Id == projeto.Id, Ct));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var leitura = ContextoDoCadastro.NovoLeitura(_infra);
            (await leitura.Set<DestinoEntity>().AnyAsync(d => d.ProjetoId == projeto.Id, Ct)).Should().BeFalse();
            (await leitura.Set<AmbienteProjetoEntity>().AnyAsync(a => a.ProjetoId == projeto.Id, Ct)).Should().BeFalse();
        }

        // ---- Flag (critério 17) ----

        [Theory, DisplayName("Critério 17: flag desligado responde 404 em todas as rotas novas")]
        [InlineData("GET", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/gitlab/projetos")]
        [InlineData("POST", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/projetos")]
        [InlineData("PUT", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        [InlineData("PUT", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff/dependencias")]
        [InlineData("PUT", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff/ambientes/qa")]
        [InlineData("POST", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff/ambientes/qa/destinos")]
        [InlineData("PUT", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff/ambientes/qa/destinos/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        [InlineData("DELETE", "/api/v1/projetos/6f9619ff-8b86-d011-b42d-00c04fc964ff/ambientes/qa/destinos/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        public async Task FlagDesligado_404(string metodo, string url)
        {
            var sessao = await SessaoAsync(_desligada, "sub-flag-off-proj", "mor");

            var resposta = await EnviarAsync(_desligada, new HttpMethod(metodo), url, sessao, metodo is "GET" or "DELETE" ? null : new { });

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
