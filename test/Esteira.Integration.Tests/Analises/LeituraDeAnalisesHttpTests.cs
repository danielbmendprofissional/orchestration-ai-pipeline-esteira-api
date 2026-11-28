using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Entregas;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Analises
{
    // E2.4, critérios 12, 13 e 18: leitura das Análises e dos Achados com sessão, PostgreSQL real, Features:E2.
    [Trait("Fatia", "E2")]
    public sealed class LeituraDeAnalisesHttpTests : IClassFixture<ApiDeTeste>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public LeituraDeAnalisesHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => b.UseSetting("Features:E2", "true"));
            _desligada = api.WithWebHostBuilder(b => b.UseSetting("Features:E2", ""));
        }

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static Task<HttpResponseMessage> GetAsync(WebApplicationFactory<Program> api, string url, string? sessao, int? pagina = null, int? tamanho = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            if (pagina is not null)
                pedido.Headers.Add("X-Page", pagina.ToString());
            if (tamanho is not null)
                pedido.Headers.Add("X-Page-Size", tamanho.ToString());
            return cliente.SendAsync(pedido, Ct);
        }

        private async Task<(CenarioDeEntrega Cenario, string Sub)> CenarioAsync()
        {
            var c = await CenarioDeEntrega.CriarAsync(_infra);
            var sub = $"sub-e2-{Guid.NewGuid():N}";
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            ctx.Add(PapelAplicacaoEntity.Criar(c.Aplicacao.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return (c, sub);
        }

        private async Task<AnaliseEntity> GravarAsync(
            CenarioDeEntrega c, string fonte, long pipeline, params (SeveridadeAchado Sev, string Titulo)[] achados)
        {
            var a = AnaliseEntity.Criar(c.Fluxo().Id, AmbienteDeploy.Qa, EscopoAnalise.Artefato, null, fonte, TipoAnalise.Ferramenta,
                VereditoAnalise.Reprovado, null, pipeline, T0.AddMinutes(pipeline),
                achados.Select(x => new NovoAchado(x.Sev, x.Titulo, null, "a.cs", 1, "<script>alert(1)</script>")).ToList()).Value;
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            ctx.Add(a);
            await ctx.SaveChangesAsync(Ct);
            return a;
        }

        private static string Etapa(CenarioDeEntrega c, string etapa = "qa")
            => $"/api/v1/entregas/{c.Entrega.PublicId}/fluxos/{c.Fluxo().PublicId}/etapas/{etapa}/analises";

        private static string Achados(CenarioDeEntrega c, AnaliseEntity a)
            => $"/api/v1/entregas/{c.Entrega.PublicId}/fluxos/{c.Fluxo().PublicId}/analises/{a.PublicId}/achados";

        private static async Task<string> CodigoAsync(HttpResponseMessage r)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;
        }

        [Fact, DisplayName("Critério 12: Features:E2 desligado responde 404 de rota nas duas leituras")]
        public async Task FlagDesligada()
        {
            var (c, sub) = await CenarioAsync();
            var a = await GravarAsync(c, "opengrep", 1, (SeveridadeAchado.Alta, "x"));
            var sessao = await SessaoAsync(_desligada, sub);

            var etapa = await GetAsync(_desligada, Etapa(c), sessao);
            var achados = await GetAsync(_desligada, Achados(c, a), sessao);

            etapa.StatusCode.Should().Be(HttpStatusCode.NotFound);
            achados.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await etapa.Content.ReadAsStringAsync(Ct)).Should().NotContain("entrega.nao-encontrada");
        }

        [Fact, DisplayName("Critério 12: Etapa sem Análise devolve analises vazia e gate nulo")]
        public async Task EtapaVazia()
        {
            var (c, sub) = await CenarioAsync();

            var r = await GetAsync(_ligada, Etapa(c), await SessaoAsync(_ligada, sub));

            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = JsonNode.Parse(await r.Content.ReadAsStringAsync(Ct))!;
            corpo["analises"]!.AsArray().Should().BeEmpty();
            (corpo.AsObject().ContainsKey("gate") ? corpo["gate"] : null).Should().BeNull();
        }

        [Fact, DisplayName("Critério 12: devolve só as Análises da última pipeline do Ambiente, com contagem por severidade e a decisão")]
        public async Task UltimaPipeline()
        {
            var (c, sub) = await CenarioAsync();
            await GravarAsync(c, "trivy-fs", 1, (SeveridadeAchado.Critica, "velha"));
            await GravarAsync(c, "opengrep", 2, (SeveridadeAchado.Alta, "a"), (SeveridadeAchado.Alta, "b"), (SeveridadeAchado.Info, "i"));
            await GravarAsync(c, "checkov", 2, (SeveridadeAchado.Media, "m"));
            await using (var ctx = ContextoDoCadastro.Novo(_infra))
            {
                ctx.Add(DecisaoDoGateEntity.Criar(c.Fluxo().Id, AmbienteDeploy.Qa, MomentoDoGate.Gate,
                    new AvaliacaoDoGate(DecisaoGate.Bloqueado, new ContagemDeSeveridade(0, 2, 1, 0), MotivosDoGate.Bloqueado), 2, T0));
                ctx.Add(DecisaoDoGateEntity.Criar(c.Fluxo().Id, AmbienteDeploy.Qa, MomentoDoGate.Gate,
                    new AvaliacaoDoGate(DecisaoGate.Liberado, new ContagemDeSeveridade(0, 0, 0, 0), null), 1, T0));
                await ctx.SaveChangesAsync(Ct);
            }

            var r = await GetAsync(_ligada, Etapa(c), await SessaoAsync(_ligada, sub));

            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = JsonNode.Parse(await r.Content.ReadAsStringAsync(Ct))!;
            corpo["gate"]!["momento"]!.GetValue<string>().Should().Be("gate");
            corpo["gate"]!["decisao"]!.GetValue<string>().Should().Be("bloqueado");
            corpo["gate"]!["motivo"]!.GetValue<string>().Should().Be("gate.bloqueado");
            corpo["gate"]!["contagem"]!["alta"]!.GetValue<int>().Should().Be(2);
            var analises = corpo["analises"]!.AsArray();
            analises.Select(a => a!["fonte"]!.GetValue<string>()).Should().BeEquivalentTo(["opengrep", "checkov"]);
            var og = analises.Single(a => a!["fonte"]!.GetValue<string>() == "opengrep")!;
            og["escopo"]!.GetValue<string>().Should().Be("artefato");
            og["tipo"]!.GetValue<string>().Should().Be("ferramenta");
            og["veredito"]!.GetValue<string>().Should().Be("reprovado");
            og["contagem"]!["alta"]!.GetValue<int>().Should().Be(2);
            og["contagem"]!["info"]!.GetValue<int>().Should().Be(1);
            og["id"]!.GetValue<Guid>().Should().NotBeEmpty();
        }

        [Fact, DisplayName("Critério 12: Etapa de outro Ambiente não enxerga as Análises do qa")]
        public async Task OutroAmbiente()
        {
            var (c, sub) = await CenarioAsync();
            await GravarAsync(c, "opengrep", 1, (SeveridadeAchado.Alta, "a"));

            var r = await GetAsync(_ligada, Etapa(c, "hml"), await SessaoAsync(_ligada, sub));

            JsonNode.Parse(await r.Content.ReadAsStringAsync(Ct))!["analises"]!.AsArray().Should().BeEmpty();
        }

        [Fact, DisplayName("Critério 12: MOR sem papel lê; sem sessão 401")]
        public async Task MorESemSessao()
        {
            var (c, _) = await CenarioAsync();

            (await GetAsync(_ligada, Etapa(c), await SessaoAsync(_ligada, $"sub-mor-{Guid.NewGuid():N}", "mor"))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await GetAsync(_ligada, Etapa(c), null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact, DisplayName("Critério 13: Achados paginados com X-*, da mais grave para a menos, e filtro por severidade")]
        public async Task AchadosPaginadosEFiltrados()
        {
            var (c, sub) = await CenarioAsync();
            var a = await GravarAsync(c, "opengrep", 1,
                (SeveridadeAchado.Baixa, "b1"), (SeveridadeAchado.Critica, "c1"), (SeveridadeAchado.Info, "i1"),
                (SeveridadeAchado.Alta, "a1"), (SeveridadeAchado.Media, "m1"), (SeveridadeAchado.Alta, "a2"));
            var sessao = await SessaoAsync(_ligada, sub);

            var p1 = await GetAsync(_ligada, Achados(c, a), sessao, pagina: 1, tamanho: 4);
            var p2 = await GetAsync(_ligada, Achados(c, a), sessao, pagina: 2, tamanho: 4);
            var filtrado = await GetAsync(_ligada, Achados(c, a) + "?severidade=alta", sessao);

            p1.StatusCode.Should().Be(HttpStatusCode.OK);
            p1.Headers.GetValues("X-Total-Count").Single().Should().Be("6");
            p1.Headers.GetValues("X-Total-Pages").Single().Should().Be("2");
            p1.Headers.GetValues("X-Page").Single().Should().Be("1");
            p1.Headers.GetValues("X-Page-Size").Single().Should().Be("4");
            var sev1 = JsonNode.Parse(await p1.Content.ReadAsStringAsync(Ct))!.AsArray();
            sev1.Select(x => x!["severidade"]!.GetValue<string>()).Should().Equal("critica", "alta", "alta", "media");
            var sev2 = JsonNode.Parse(await p2.Content.ReadAsStringAsync(Ct))!.AsArray();
            sev2.Select(x => x!["severidade"]!.GetValue<string>()).Should().Equal("baixa", "info");
            sev1[0]!["descricao"]!.GetValue<string>().Should().Be("<script>alert(1)</script>");
            sev1[0]!["arquivo"]!.GetValue<string>().Should().Be("a.cs");
            JsonNode.Parse(await filtrado.Content.ReadAsStringAsync(Ct))!.AsArray().Should().HaveCount(2);
        }

        [Fact, DisplayName("Critério 13: severidade desconhecida no filtro responde 400 analise.severidade-invalida")]
        public async Task SeveridadeInvalida()
        {
            var (c, sub) = await CenarioAsync();
            var a = await GravarAsync(c, "opengrep", 1, (SeveridadeAchado.Alta, "x"));

            var r = await GetAsync(_ligada, Achados(c, a) + "?severidade=gravissima", await SessaoAsync(_ligada, sub));

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoAsync(r)).Should().Be("analise.severidade-invalida");
        }

        [Fact, DisplayName("Critério 13: Entrega, Fluxo, Análise inexistentes ou sem acesso respondem o mesmo 404 entrega.nao-encontrada")]
        public async Task NaoEncontradaUniforme()
        {
            var (c, sub) = await CenarioAsync();
            var (_, subOutra) = await CenarioAsync();
            var a = await GravarAsync(c, "opengrep", 1, (SeveridadeAchado.Alta, "x"));
            var sessao = await SessaoAsync(_ligada, sub);
            var alheia = await SessaoAsync(_ligada, subOutra);
            var entrega = c.Entrega.PublicId;
            var fluxo = c.Fluxo().PublicId;

            var respostas = new[]
            {
                await GetAsync(_ligada, Etapa(c), alheia),
                await GetAsync(_ligada, Achados(c, a), alheia),
                await GetAsync(_ligada, $"/api/v1/entregas/{Guid.NewGuid()}/fluxos/{fluxo}/etapas/qa/analises", sessao),
                await GetAsync(_ligada, $"/api/v1/entregas/{entrega}/fluxos/{Guid.NewGuid()}/etapas/qa/analises", sessao),
                await GetAsync(_ligada, $"/api/v1/entregas/{entrega}/fluxos/{fluxo}/analises/{Guid.NewGuid()}/achados", sessao),
                await GetAsync(_ligada, $"/api/v1/entregas/{entrega}/fluxos/{fluxo}/etapas/abertura/analises", sessao),
            };

            foreach (var r in respostas)
            {
                r.StatusCode.Should().Be(HttpStatusCode.NotFound);
                (await CodigoAsync(r)).Should().Be("entrega.nao-encontrada");
            }
        }

        [Fact, DisplayName("Critério 18: OpenAPI v1 lista as duas leituras e pipeline-v2 só tem rotas /api/pipeline/v2 com abrir e gate")]
        public async Task OpenApi()
        {
            var cliente = _ligada.CreateClient();
            var v1 = JsonNode.Parse(await cliente.GetStringAsync("/openapi/v1.json", Ct))!;
            var v2 = JsonNode.Parse(await cliente.GetStringAsync("/openapi/pipeline-v2.json", Ct))!;

            foreach (var rota in new[]
                { "/api/v1/entregas/{entregaId}/fluxos/{fluxoId}/etapas/{etapa}/analises", "/api/v1/entregas/{entregaId}/fluxos/{fluxoId}/analises/{analiseId}/achados" })
            {
                var respostas = v1["paths"]![rota]!["get"]!["responses"]!.AsObject();
                respostas.ContainsKey("200").Should().BeTrue();
                foreach (var status in new[] { "401", "404" })
                    respostas[status]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
            }
            v1["paths"]!["/api/v1/entregas/{entregaId}/fluxos/{fluxoId}/analises/{analiseId}/achados"]!["get"]!["responses"]!.AsObject().ContainsKey("400").Should().BeTrue();
            var schemas = v1["components"]!["schemas"]!.AsObject();
            schemas["AnalisesDaEtapaDto"]!["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo(["gate", "analises"]);
            schemas["AchadoDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().BeEquivalentTo(["severidade", "titulo", "regraId", "arquivo", "linha", "descricao"]);

            var rotas = v2["paths"]!.AsObject().Select(p => p.Key).ToList();
            rotas.Should().OnlyContain(r => r.StartsWith("/api/pipeline/v2/"));
            rotas.Should().Contain(["/api/pipeline/v2/abrir", "/api/pipeline/v2/gate"]);
            var gate = v2["paths"]!["/api/pipeline/v2/gate"]!["post"]!["responses"]!.AsObject();
            gate.Select(p => p.Key).Should().Contain(["200", "401", "403", "409", "422"]);
        }
    }
}
