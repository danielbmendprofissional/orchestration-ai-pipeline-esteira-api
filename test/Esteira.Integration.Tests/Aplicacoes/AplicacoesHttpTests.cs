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
    // A1.3 na borda HTTP: criar/listar/detalhar/alterar Aplicação e alterar Política, com sessão simulada,
    // IGitLab falso, flag Features:A1, mapa de status da ADR-0003 e auditoria na mesma transação.
    [Trait("Fatia", "A1")]
    public sealed class AplicacoesHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class GitLabFalso : IGitLab
        {
            public Dictionary<string, GrupoGitLabDto> Grupos { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Task<Result<GrupoGitLabDto>> ObterGrupoAsync(string caminho, CancellationToken cancellationToken = default)
                => Task.FromResult(Grupos.TryGetValue(caminho, out var g)
                    ? Result<GrupoGitLabDto>.Sucesso(g)
                    : Result<GrupoGitLabDto>.Falha(new Erro("gitlab.grupo-nao-encontrado", "Grupo não encontrado.", TipoErro.Failure)));

            public Task<Result<IReadOnlyList<ProjetoGitLabDto>>> ListarProjetosDoGrupoAsync(string grupoCaminho, long grupoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<string?>> ObterArquivoAsync(string grupoCaminho, long projetoId, string caminho, string referencia, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<IssueGitLabDto>>> ListarIssuesAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<IReadOnlyList<MergeRequestGitLabDto>>> ListarMergeRequestsAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Result<ProjetoDoGrupoGitLabDto?>> ObterProjetoAsync(string grupoCaminho, long projetoId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        private readonly InfraestruturaFixture _infra;
        private readonly GitLabFalso _gitlab = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public AplicacoesHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
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
            WebApplicationFactory<Program> api, HttpMethod metodo, string url, string? sessao, object? corpo = null, params (string, string)[] headers)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            foreach (var (k, v) in headers)
                pedido.Headers.Add(k, v);
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

        private string NovoGrupo(out long id)
        {
            var caminho = $"g{Guid.NewGuid():N}"[..12];
            id = ContextoDoCadastro.IdUnico();
            _gitlab.Grupos[caminho] = new GrupoGitLabDto(id, caminho, caminho);
            return caminho;
        }

        private async Task<Guid> AplicacaoAsync(string? nome = null, string? sigla = null, bool ativa = true, params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar(nome ?? "App", sigla ?? ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            if (!ativa)
                app.Alterar(app.Nome, app.Sigla, false);
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                ctx.Add(PoliticaSeveridadeEntity.Padrao(app.Id, amb));
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        private async Task<List<RegistroAuditoriaEntity>> AuditoriaAsync(Guid publicId)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            return await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == publicId).ToListAsync(Ct);
        }

        // ---- POST /aplicacoes ----

        [Fact, DisplayName("Critério 1: MOR cria Aplicação (201), com ID do grupo guardado, 4 políticas padrão e auditoria")]
        public async Task Criar_Mor()
        {
            var grupo = NovoGrupo(out var grupoId);
            var sigla = ContextoDoCadastro.SiglaUnica().ToLowerInvariant();
            var sessao = await SessaoAsync(_ligada, "sub-mor-criar", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao, new { nome = "Nova", sigla, grupoGitLab = grupo });

            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            var corpo = await CorpoAsync(resposta);
            var id = corpo.GetProperty("id").GetGuid();
            corpo.GetProperty("sigla").GetString().Should().Be(sigla.ToUpperInvariant());
            resposta.Headers.Location!.ToString().Should().EndWith($"/api/v1/aplicacoes/{id}");

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var app = await ctx.Set<AplicacaoEntity>().SingleAsync(a => a.PublicId == id, Ct);
            app.GrupoGitLabId.Should().Be(grupoId);
            var politicas = await ctx.Set<PoliticaSeveridadeEntity>().Where(p => p.AplicacaoId == app.Id).ToListAsync(Ct);
            politicas.Select(p => p.Ambiente).Should().BeEquivalentTo(Enum.GetValues<AmbientePolitica>());

            var auditoria = (await AuditoriaAsync(id)).Should().ContainSingle().Subject;
            (auditoria.Usuario, auditoria.Acao, auditoria.Entidade).Should().Be(("sub-mor-criar", AcaoAuditoria.Criou, "Aplicacao"));
            auditoria.Antes.Should().BeNull();
        }

        [Fact, DisplayName("Critério 2: sigla repetida responde 409 aplicacao.sigla-duplicada")]
        public async Task Criar_SiglaDuplicada()
        {
            var sigla = ContextoDoCadastro.SiglaUnica();
            await AplicacaoAsync(sigla: sigla.ToUpperInvariant());
            var grupo = NovoGrupo(out _);
            var sessao = await SessaoAsync(_ligada, "sub-mor-dup", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao, new { nome = "X", sigla = sigla.ToLowerInvariant(), grupoGitLab = grupo });

            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.sigla-duplicada");
        }

        [Fact, DisplayName("Critério 3: grupo já vinculado responde 409 aplicacao.grupo-ja-vinculado")]
        public async Task Criar_GrupoJaVinculado()
        {
            var grupo = NovoGrupo(out _);
            var sessao = await SessaoAsync(_ligada, "sub-mor-grupo", "mor");
            (await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao,
                new { nome = "A", sigla = ContextoDoCadastro.SiglaUnica(), grupoGitLab = grupo })).StatusCode.Should().Be(HttpStatusCode.Created);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao,
                new { nome = "B", sigla = ContextoDoCadastro.SiglaUnica(), grupoGitLab = grupo });

            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.grupo-ja-vinculado");
        }

        [Fact, DisplayName("Critério 4: grupo inexistente responde 422 gitlab.grupo-nao-encontrado e não grava nada")]
        public async Task Criar_GrupoInexistente()
        {
            var sigla = ContextoDoCadastro.SiglaUnica();
            var sessao = await SessaoAsync(_ligada, "sub-mor-sem-grupo", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao, new { nome = "X", sigla, grupoGitLab = "nao-existe" });

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("gitlab.grupo-nao-encontrado");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            (await ctx.Set<AplicacaoEntity>().AnyAsync(a => a.Sigla == sigla.ToUpperInvariant(), Ct)).Should().BeFalse();
        }

        [Fact, DisplayName("Corpo sem nome responde 400 com code estável")]
        public async Task Criar_Validacao()
        {
            var sessao = await SessaoAsync(_ligada, "sub-mor-val", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao, new { sigla = "X", grupoGitLab = "g" });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.nome-obrigatorio");
        }

        [Fact, DisplayName("Critério 13: quem não é MOR recebe 403 acesso.papel-insuficiente ao criar")]
        public async Task Criar_NaoMor()
        {
            var grupo = NovoGrupo(out _);
            var sessao = await SessaoAsync(_ligada, "sub-comum");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, "/api/v1/aplicacoes", sessao, new { nome = "X", sigla = ContextoDoCadastro.SiglaUnica(), grupoGitLab = grupo });

            resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(resposta)).Should().Be("acesso.papel-insuficiente");
        }

        [Theory, DisplayName("Critério 17: flag desligado responde 404 em todas as rotas da Aplicação")]
        [InlineData("GET", "/api/v1/aplicacoes")]
        [InlineData("POST", "/api/v1/aplicacoes")]
        [InlineData("GET", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        [InlineData("PUT", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        [InlineData("PUT", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/politicas/qa")]
        public async Task FlagDesligado_404(string metodo, string url)
        {
            var sessao = await SessaoAsync(_desligada, "sub-flag-off", "mor");

            var resposta = await EnviarAsync(_desligada, new HttpMethod(metodo), url, sessao, metodo == "GET" ? null : new { });

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Sem sessão: 401")]
        public async Task SemSessao_401()
        {
            (await EnviarAsync(_ligada, HttpMethod.Get, "/api/v1/aplicacoes", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // ---- GET /aplicacoes ----

        [Fact, DisplayName("Lista paginada (ADR-0002): headers X-*, ordenação por nome e filtros busca/ativa")]
        public async Task Listar_Paginada()
        {
            var marca = $"Lst{Guid.NewGuid():N}"[..10];
            foreach (var n in new[] { "C", "A", "B" })
                await AplicacaoAsync(nome: $"{marca}-{n}");
            await AplicacaoAsync(nome: $"{marca}-D", ativa: false);
            var sessao = await SessaoAsync(_ligada, "sub-mor-lista", "mor");

            var pagina = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes?busca={marca}&sort=nome&order=desc", sessao,
                headers: [("X-Page", "1"), ("X-Page-Size", "2")]);

            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            pagina.Headers.GetValues("X-Total-Count").Single().Should().Be("4");
            pagina.Headers.GetValues("X-Total-Pages").Single().Should().Be("2");
            pagina.Headers.GetValues("X-Page").Single().Should().Be("1");
            pagina.Headers.GetValues("X-Page-Size").Single().Should().Be("2");
            var itens = await CorpoAsync(pagina);
            itens.EnumerateArray().Select(i => i.GetProperty("nome").GetString()).Should().Equal($"{marca}-D", $"{marca}-C");
            var primeiro = itens[0];
            primeiro.TryGetProperty("sigla", out _).Should().BeTrue();
            primeiro.TryGetProperty("grupo", out _).Should().BeTrue();
            primeiro.GetProperty("quantidadeProjetos").GetInt32().Should().Be(0);
            primeiro.GetProperty("ativa").GetBoolean().Should().BeFalse();

            var inativas = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes?busca={marca}&ativa=false", sessao);
            (await CorpoAsync(inativas)).GetArrayLength().Should().Be(1);
            var ativas = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes?busca={marca}&ativa=true", sessao);
            (await CorpoAsync(ativas)).GetArrayLength().Should().Be(3);
        }

        [Fact, DisplayName("Critério 12: quem não é MOR lista só as Aplicações em que tem papel")]
        public async Task Listar_SoComPapel()
        {
            var marca = $"Vis{Guid.NewGuid():N}"[..10];
            var sub = $"sub-{marca}";
            await AplicacaoAsync(nome: $"{marca}-minha", papeis: (sub, PapelAplicacao.Membro));
            await AplicacaoAsync(nome: $"{marca}-alheia");
            var comum = await SessaoAsync(_ligada, sub);
            var mor = await SessaoAsync(_ligada, "sub-mor-vis", "mor");

            var doComum = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes?busca={marca}", comum));
            var doMor = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes?busca={marca}", mor));

            doComum.EnumerateArray().Select(i => i.GetProperty("nome").GetString()).Should().Equal($"{marca}-minha");
            doMor.GetArrayLength().Should().Be(2);
        }

        [Fact, DisplayName("Paginação inválida responde 400 antes de consultar")]
        public async Task Listar_PaginaInvalida()
        {
            var sessao = await SessaoAsync(_ligada, "sub-mor-pg", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Get, "/api/v1/aplicacoes", sessao, headers: [("X-Page", "0")]);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("paginacao.pagina-invalida");
        }

        // ---- GET /aplicacoes/{id} ----

        [Fact, DisplayName("Detalhe: quem tem papel recebe a Aplicação com as 4 políticas e listas extensíveis")]
        public async Task Detalhe_ComPapel()
        {
            var sub = $"sub-det-{Guid.NewGuid():N}";
            var id = await AplicacaoAsync(papeis: (sub, PapelAplicacao.Membro));
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{id}", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            corpo.GetProperty("id").GetGuid().Should().Be(id);
            corpo.GetProperty("politicas").EnumerateArray().Select(p => p.GetProperty("ambiente").GetString())
                .Should().BeEquivalentTo("review", "qa", "hml", "producao");
            var qa = corpo.GetProperty("politicas").EnumerateArray().Single(p => p.GetProperty("ambiente").GetString() == "qa");
            (qa.GetProperty("modo").GetString(), qa.GetProperty("critica").GetInt32(), qa.GetProperty("alta").GetInt32()).Should().Be(("bloqueia", 0, 0));
            corpo.GetProperty("projetos").GetArrayLength().Should().Be(0);
            corpo.GetProperty("papeis").GetArrayLength().Should().Be(1);
        }

        [Fact, DisplayName("Critério 12: sem papel e sem MOR o detalhe é 404 aplicacao.nao-encontrada, nunca 403")]
        public async Task Detalhe_SemPapel()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-sem-papel-det");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{id}", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.nao-encontrada");
        }

        // ---- PUT /aplicacoes/{id} ----

        [Fact, DisplayName("MOR altera nome, sigla e ativa (200) e a auditoria guarda antes e depois")]
        public async Task Alterar_Mor()
        {
            var id = await AplicacaoAsync(nome: "Antes", sigla: ContextoDoCadastro.SiglaUnica());
            var novaSigla = ContextoDoCadastro.SiglaUnica();
            var sessao = await SessaoAsync(_ligada, "sub-mor-alt", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}", sessao, new { nome = "Depois", sigla = novaSigla, ativa = false });

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            (corpo.GetProperty("nome").GetString(), corpo.GetProperty("ativa").GetBoolean()).Should().Be(("Depois", false));
            var registro = (await AuditoriaAsync(id)).Should().ContainSingle().Subject;
            registro.Acao.Should().Be(AcaoAuditoria.Alterou);
            JsonDocument.Parse(registro.Antes!).RootElement.GetProperty("nome").GetString().Should().Be("Antes");
            JsonDocument.Parse(registro.Depois!).RootElement.GetProperty("nome").GetString().Should().Be("Depois");
        }

        [Fact, DisplayName("Alterar para sigla de outra Aplicação responde 409 e não deixa auditoria")]
        public async Task Alterar_SiglaDuplicada()
        {
            var outra = ContextoDoCadastro.SiglaUnica();
            await AplicacaoAsync(sigla: outra);
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-alt-dup", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}", sessao, new { nome = "X", sigla = outra, ativa = true });

            resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.sigla-duplicada");
            (await AuditoriaAsync(id)).Should().BeEmpty();
        }

        [Fact, DisplayName("Critério 13: analista não altera a Aplicação (403); sem papel é 404")]
        public async Task Alterar_SemPermissao()
        {
            var sub = $"sub-ana-{Guid.NewGuid():N}";
            var id = await AplicacaoAsync(papeis: (sub, PapelAplicacao.Analista));
            var analista = await SessaoAsync(_ligada, sub);
            var estranho = await SessaoAsync(_ligada, "sub-estranho-alt");
            var corpo = new { nome = "X", sigla = ContextoDoCadastro.SiglaUnica(), ativa = true };

            var r403 = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}", analista, corpo);
            var r404 = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}", estranho, corpo);

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // ---- PUT /aplicacoes/{id}/politicas/{ambiente} ----

        [Fact, DisplayName("Aprovador técnico endurece a política (200) e a auditoria guarda antes e depois")]
        public async Task Politica_Endurecer()
        {
            var sub = $"sub-apr-{Guid.NewGuid():N}";
            var id = await AplicacaoAsync(papeis: (sub, PapelAplicacao.AprovadorTecnico));
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/review", sessao,
                new { critica = 0, alta = 0, media = 5, baixa = (int?)null, modo = "bloqueia" });

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            (corpo.GetProperty("ambiente").GetString(), corpo.GetProperty("modo").GetString(), corpo.GetProperty("media").GetInt32())
                .Should().Be(("review", "bloqueia", 5));

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var politica = await ctx.Set<PoliticaSeveridadeEntity>().SingleAsync(p => p.PublicId == corpo.GetProperty("id").GetGuid(), Ct);
            (politica.Modo, politica.Media, politica.Baixa).Should().Be((ModoPolitica.Bloqueia, 5, (int?)null));
            var registro = (await AuditoriaAsync(politica.PublicId)).Should().ContainSingle().Subject;
            (registro.Entidade, registro.Usuario).Should().Be(("PoliticaSeveridade", sub));
            JsonDocument.Parse(registro.Antes!).RootElement.GetProperty("modo").GetString().Should().Be("avisa");
            JsonDocument.Parse(registro.Depois!).RootElement.GetProperty("modo").GetString().Should().Be("bloqueia");
        }

        [Fact, DisplayName("Critério 11a: afrouxar o piso de producao responde 422 politica.abaixo-do-minimo e não grava")]
        public async Task Politica_AbaixoDoMinimo()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-piso", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/producao", sessao,
                new { critica = 2, alta = 0, modo = "bloqueia" });

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("politica.abaixo-do-minimo");
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var app = await ctx.Set<AplicacaoEntity>().SingleAsync(a => a.PublicId == id, Ct);
            (await ctx.Set<PoliticaSeveridadeEntity>().SingleAsync(p => p.AplicacaoId == app.Id && p.Ambiente == AmbientePolitica.Producao, Ct))
                .Critica.Should().Be(0);
            (await ctx.Set<RegistroAuditoriaEntity>().AnyAsync(r => r.Entidade == "PoliticaSeveridade" && r.Usuario == "sub-mor-piso", Ct)).Should().BeFalse();
        }

        [Fact, DisplayName("Critério 11: limite negativo responde 400 politica.limite-negativo")]
        public async Task Politica_Negativo()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-neg", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/review", sessao,
                new { media = -1, modo = "avisa" });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("politica.limite-negativo");
        }

        [Fact, DisplayName("Ambiente fora do vocabulário (dev) responde 400 politica.ambiente-invalido")]
        public async Task Politica_AmbienteInvalido()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-amb", "mor");

            var resposta = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/dev", sessao, new { modo = "avisa" });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("politica.ambiente-invalido");
        }

        [Fact, DisplayName("Critério 13: analista não altera política (403); sem papel é 404")]
        public async Task Politica_SemPermissao()
        {
            var sub = $"sub-ana-pol-{Guid.NewGuid():N}";
            var id = await AplicacaoAsync(papeis: (sub, PapelAplicacao.Analista));
            var analista = await SessaoAsync(_ligada, sub);
            var estranho = await SessaoAsync(_ligada, "sub-estranho-pol");
            var corpo = new { critica = 0, alta = 0, modo = "bloqueia" };

            var r403 = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/qa", analista, corpo);
            var r404 = await EnviarAsync(_ligada, HttpMethod.Put, $"/api/v1/aplicacoes/{id}/politicas/qa", estranho, corpo);

            r403.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(r403)).Should().Be("acesso.papel-insuficiente");
            r404.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
