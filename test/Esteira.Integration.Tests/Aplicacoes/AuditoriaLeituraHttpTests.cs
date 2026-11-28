using System.ComponentModel;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // X1.3, critérios 2, 3, 4, 6, 7 e 12: leitura da trilha na borda HTTP, PostgreSQL real, sessão simulada.
    [Trait("Fatia", "X1")]
    public sealed class AuditoriaLeituraHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;
        private readonly WebApplicationFactory<Program> _ausente;

        public AuditoriaLeituraHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => b.UseSetting("Features:X1", "true"));
            _desligada = api.WithWebHostBuilder(b => b.UseSetting("Features:X1", "false"));
            _ausente = api.WithWebHostBuilder(_ => { });
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static string SubUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}"[..20];

        private async Task<AplicacaoEntity> AplicacaoAsync(string? nome = null, params string[] subsComPapel)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar(nome ?? "App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var sub in subsComPapel)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return app;
        }

        private async Task<Guid> RegistroAsync(
            long? aplicacaoId, CategoriaAuditoria categoria = CategoriaAuditoria.Cadastro, OrigemAuditoria origem = OrigemAuditoria.Tela,
            string usuario = "sub-x", string? nome = "Fulana", string? verbo = null, string? objetoCodigo = null, string? motivo = null,
            DateTime? quando = null, string? depois = null, string entidade = "Projeto", AcaoAuditoria acao = AcaoAuditoria.Criou)
        {
            var publicId = Guid.CreateVersion7();
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            ctx.Add(RegistroAuditoriaEntity.Criar(quando ?? DateTime.UtcNow, usuario, entidade, publicId, acao, null, depois ?? """{"nome":"x"}""",
                categoria, origem, aplicacaoId, nome, verbo, objetoCodigo, null, motivo));
            await ctx.SaveChangesAsync(Ct);
            return publicId;
        }

        private static async Task<HttpResponseMessage> GetAsync(WebApplicationFactory<Program> api, string url, string? sessaoId, Action<HttpRequestMessage>? ajustar = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            if (sessaoId is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessaoId}");
            ajustar?.Invoke(pedido);
            return await cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private async Task<JsonElement> ListaAsync(string sub, string query, params string[] perfis)
        {
            var sessao = await SessaoAsync(_ligada, sub, perfis);
            var r = await GetAsync(_ligada, $"/api/v1/auditoria{query}", sessao);
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            return await CorpoAsync(r);
        }

        private static string[] Objetos(JsonElement lista)
            => [.. lista.EnumerateArray().Select(i => i.GetProperty("objeto").GetProperty("codigo").GetString()!)];

        [Theory, DisplayName("12: flag ausente ou desligada -> 404 nas três rotas de leitura")]
        [InlineData("/api/v1/auditoria", false)]
        [InlineData("/api/v1/auditoria/categorias", false)]
        [InlineData("/api/v1/auditoria/filtros", false)]
        [InlineData("/api/v1/auditoria", true)]
        [InlineData("/api/v1/auditoria/categorias", true)]
        [InlineData("/api/v1/auditoria/filtros", true)]
        public async Task FlagDesligada_404(string url, bool ausente)
        {
            var api = ausente ? _ausente : _desligada;
            var sessao = await SessaoAsync(api, SubUnico("flag"), "mor");

            (await GetAsync(api, url, sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("12: flag ligada responde 200 nas três rotas; sem sessão responde 401")]
        public async Task FlagLigada_200()
        {
            var sessao = await SessaoAsync(_ligada, SubUnico("flag"), "mor");

            foreach (var url in new[] { "/api/v1/auditoria", "/api/v1/auditoria/categorias", "/api/v1/auditoria/filtros" })
                (await GetAsync(_ligada, url, sessao)).StatusCode.Should().Be(HttpStatusCode.OK, url);

            (await GetAsync(_ligada, "/api/v1/auditoria", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact, DisplayName("2 e 4: pessoa vê só registros das Aplicações em que tem papel, do mais recente para o mais antigo")]
        public async Task Pessoa_VeSoAsSuas_Ordenado()
        {
            var sub = SubUnico("membro");
            var minha = await AplicacaoAsync("Minha", sub);
            var alheia = await AplicacaoAsync("Alheia");
            var t = DateTime.UtcNow;
            await RegistroAsync(minha.Id, objetoCodigo: "ANTIGO", quando: t.AddMinutes(-10));
            await RegistroAsync(minha.Id, objetoCodigo: "NOVO", quando: t.AddMinutes(-1));
            await RegistroAsync(alheia.Id, objetoCodigo: "ALHEIO");
            await RegistroAsync(null, objetoCodigo: "SEM-APP");

            var lista = await ListaAsync(sub, "");

            Objetos(lista).Should().Equal("NOVO", "ANTIGO");
            lista[0].GetProperty("aplicacao").GetProperty("nome").GetString().Should().Be("Minha");
            lista[0].GetProperty("aplicacao").GetProperty("id").GetGuid().Should().Be(minha.PublicId);
        }

        [Fact, DisplayName("3: MOR vê todos os registros, inclusive sem Aplicação")]
        public async Task Mor_VeTudo()
        {
            var alheia = await AplicacaoAsync();
            var semApp = $"SA-{Guid.NewGuid():N}";
            var comApp = $"CA-{Guid.NewGuid():N}";
            await RegistroAsync(null, objetoCodigo: semApp);
            await RegistroAsync(alheia.Id, objetoCodigo: comApp);

            var lista = await ListaAsync(SubUnico("mor"), $"?objeto={semApp}", "mor");
            Objetos(lista).Should().Equal(semApp);
            Objetos(await ListaAsync(SubUnico("mor"), $"?objeto={comApp}", "mor")).Should().Equal(comApp);
            // Convenção do projeto (Program.cs): nulo é omitido do JSON (NullValueHandling.Ignore).
            lista[0].TryGetProperty("aplicacao", out var aplicacao).Should().BeFalse(aplicacao.ToString());
        }

        [Fact, DisplayName("4: pessoa sem papel nenhum recebe lista vazia, contagens zero e filtros vazios")]
        public async Task SemPapel_Vazio()
        {
            var app = await AplicacaoAsync();
            await RegistroAsync(app.Id);
            await RegistroAsync(null);
            var sub = SubUnico("sempapel");
            var sessao = await SessaoAsync(_ligada, sub);

            (await CorpoAsync(await GetAsync(_ligada, "/api/v1/auditoria", sessao))).GetArrayLength().Should().Be(0);
            var categorias = await CorpoAsync(await GetAsync(_ligada, "/api/v1/auditoria/categorias", sessao));
            categorias.EnumerateArray().Sum(c => c.GetProperty("total").GetInt32()).Should().Be(0);
            var filtros = await CorpoAsync(await GetAsync(_ligada, "/api/v1/auditoria/filtros", sessao));
            filtros.GetProperty("aplicacoes").GetArrayLength().Should().Be(0);
            filtros.GetProperty("pessoas").GetArrayLength().Should().Be(0);
        }

        [Fact, DisplayName("6: filtrar por Aplicação sem papel (ou inexistente) devolve lista vazia e contagens zero, sem erro")]
        public async Task FiltroDeAplicacaoSemPapel_Vazio()
        {
            var sub = SubUnico("membro");
            await AplicacaoAsync("Minha", sub);
            var alheia = await AplicacaoAsync();
            await RegistroAsync(alheia.Id);
            var sessao = await SessaoAsync(_ligada, sub);

            foreach (var id in new[] { alheia.PublicId, Guid.NewGuid() })
            {
                var lista = await GetAsync(_ligada, $"/api/v1/auditoria?aplicacaoId={id}", sessao);
                lista.StatusCode.Should().Be(HttpStatusCode.OK);
                (await CorpoAsync(lista)).GetArrayLength().Should().Be(0);

                var categorias = await GetAsync(_ligada, $"/api/v1/auditoria/categorias?aplicacaoId={id}", sessao);
                categorias.StatusCode.Should().Be(HttpStatusCode.OK);
                (await CorpoAsync(categorias)).EnumerateArray().Sum(c => c.GetProperty("total").GetInt32()).Should().Be(0);
            }
        }

        [Fact, DisplayName("2: paginação pelos headers (ADR-0002): X-Page, X-Page-Size, X-Total-Count, X-Total-Pages")]
        public async Task Paginacao_PorHeaders()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync("P", sub);
            var t = DateTime.UtcNow;
            for (var i = 0; i < 5; i++)
                await RegistroAsync(app.Id, objetoCodigo: $"R{i}", quando: t.AddMinutes(-i));
            var sessao = await SessaoAsync(_ligada, sub);

            var r = await GetAsync(_ligada, "/api/v1/auditoria", sessao, p => { p.Headers.Add("X-Page", "2"); p.Headers.Add("X-Page-Size", "2"); });

            r.StatusCode.Should().Be(HttpStatusCode.OK);
            Objetos(await CorpoAsync(r)).Should().Equal("R2", "R3");
            r.Headers.GetValues("X-Page").Single().Should().Be("2");
            r.Headers.GetValues("X-Page-Size").Single().Should().Be("2");
            r.Headers.GetValues("X-Total-Count").Single().Should().Be("5");
            r.Headers.GetValues("X-Total-Pages").Single().Should().Be("3");
        }

        [Fact, DisplayName("7: filtros quem, categoria, periodoDias, objeto e busca aplicam em conjunto")]
        public async Task Filtros_EmConjunto()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync("F", sub);
            var t = DateTime.UtcNow;
            await RegistroAsync(app.Id, CategoriaAuditoria.Aprovacao, usuario: "sub-ana", nome: "Ana", verbo: "aprovou a Fatia V4", objetoCodigo: "V4", quando: t.AddDays(-2));
            await RegistroAsync(app.Id, CategoriaAuditoria.Aprovacao, usuario: "sub-ana", nome: "Ana", verbo: "aprovou a Fatia V4", objetoCodigo: "V4-VELHO", quando: t.AddDays(-40));
            await RegistroAsync(app.Id, CategoriaAuditoria.Fatias, usuario: "sub-ana", nome: "Ana", verbo: "reprovou a Fatia V5", objetoCodigo: "V5", motivo: "faltou teste");
            await RegistroAsync(app.Id, CategoriaAuditoria.Fatias, OrigemAuditoria.Bot, usuario: "bot-esteira", nome: "bot da Esteira", objetoCodigo: "V6");
            await RegistroAsync(app.Id, CategoriaAuditoria.Fatias, OrigemAuditoria.Pipeline, usuario: "webhook-gitlab", nome: "pipeline #7", objetoCodigo: "V7");
            await RegistroAsync(app.Id, CategoriaAuditoria.Cadastro, usuario: "sub-bia", nome: "Bia", objetoCodigo: "PROJ", depois: """{"nome":"x","replicas":2}""");

            Objetos(await ListaAsync(sub, "?quem=sub-ana&categoria=aprovacao&periodoDias=7")).Should().Equal("V4");
            Objetos(await ListaAsync(sub, "?quem=@bot")).Should().Equal("V6");
            Objetos(await ListaAsync(sub, "?quem=@pipeline")).Should().Equal("V7");
            Objetos(await ListaAsync(sub, "?objeto=V5")).Should().Equal("V5");
            Objetos(await ListaAsync(sub, "?busca=FALTOU")).Should().Equal("V5");
            Objetos(await ListaAsync(sub, "?busca=Aprovou&periodoDias=30")).Should().Equal("V4");
            Objetos(await ListaAsync(sub, "?busca=replicas")).Should().Equal("PROJ");
            Objetos(await ListaAsync(sub, "?busca=bia&categoria=cadastro")).Should().Equal("PROJ");
            (await ListaAsync(sub, "?busca=bia&categoria=fatias")).GetArrayLength().Should().Be(0);
        }

        [Fact, DisplayName("1: verbo nulo sai com o verbo padrão; resumoMudancas lista os campos")]
        public async Task VerboPadrao_EResumo()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync("V", sub);
            await RegistroAsync(app.Id, objetoCodigo: "SEM-VERBO", depois: """{"nome":"x","replicas":2}""");

            var item = (await ListaAsync(sub, "")).EnumerateArray().Single();

            item.GetProperty("verbo").GetString().Should().Be("criou o Projeto");
            item.GetProperty("resumoMudancas").GetString().Should().Be("nome, replicas");
            item.GetProperty("quem").GetProperty("nome").GetString().Should().Be("Fulana");
            item.GetProperty("quem").GetProperty("origem").GetString().Should().Be("tela");
            item.GetProperty("categoria").GetString().Should().Be("cadastro");
        }

        [Fact, DisplayName("7: /categorias conta com os mesmos filtros exceto categoria, todas as oito categorias presentes")]
        public async Task Categorias_ContamSemOFiltroDeCategoria()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync("C", sub);
            await RegistroAsync(app.Id, CategoriaAuditoria.Aprovacao, usuario: "sub-ana");
            await RegistroAsync(app.Id, CategoriaAuditoria.Aprovacao, usuario: "sub-ana");
            await RegistroAsync(app.Id, CategoriaAuditoria.Fatias, usuario: "sub-ana");
            await RegistroAsync(app.Id, CategoriaAuditoria.Fatias, usuario: "sub-bia");
            var sessao = await SessaoAsync(_ligada, sub);

            var corpo = await CorpoAsync(await GetAsync(_ligada, "/api/v1/auditoria/categorias?quem=sub-ana&categoria=fatias", sessao));

            var mapa = corpo.EnumerateArray().ToDictionary(c => c.GetProperty("categoria").GetString()!, c => c.GetProperty("total").GetInt32());
            mapa.Should().HaveCount(8);
            mapa["aprovacao"].Should().Be(2);
            mapa["fatias"].Should().Be(1);
            mapa["cadastro"].Should().Be(0);
        }

        [Fact, DisplayName("4: /filtros lista só Aplicações e pessoas dos registros visíveis, com @bot e @pipeline quando existem")]
        public async Task Filtros_SoOVisivel()
        {
            var sub = SubUnico("membro");
            var minha = await AplicacaoAsync("Minha", sub);
            var alheia = await AplicacaoAsync("Alheia");
            await RegistroAsync(minha.Id, usuario: "sub-ana", nome: "Ana");
            await RegistroAsync(minha.Id, CategoriaAuditoria.Fatias, OrigemAuditoria.Bot, usuario: "bot-esteira", nome: "bot da Esteira");
            await RegistroAsync(alheia.Id, usuario: "sub-oculta", nome: "Oculta");
            await RegistroAsync(alheia.Id, CategoriaAuditoria.Fatias, OrigemAuditoria.Pipeline, usuario: "webhook-gitlab", nome: "pipeline #1");
            var sessao = await SessaoAsync(_ligada, sub);

            var corpo = await CorpoAsync(await GetAsync(_ligada, "/api/v1/auditoria/filtros", sessao));

            corpo.GetProperty("aplicacoes").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Equal(minha.PublicId);
            corpo.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("id").GetString()).Should().BeEquivalentTo("sub-ana", "@bot");
        }

        [Theory, DisplayName("Entrada inválida: periodoDias fora de 7/30/90 e categoria desconhecida -> 400 com código kebab")]
        [InlineData("?periodoDias=15", "auditoria.periodo-invalido")]
        [InlineData("?categoria=xpto", "auditoria.categoria-invalida")]
        public async Task EntradaInvalida_400(string query, string codigo)
        {
            var sessao = await SessaoAsync(_ligada, SubUnico("mor"), "mor");

            var r = await GetAsync(_ligada, $"/api/v1/auditoria{query}", sessao);

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await r.Content.ReadAsStringAsync(Ct)).Should().Contain(codigo);
        }
    }
}
