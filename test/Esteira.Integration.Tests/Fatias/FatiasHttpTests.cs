using System.ComponentModel;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Fatias
{
    // P1.6 na borda HTTP: GET /fatias, POST /aplicacoes/{id}/fatias/atualizar e GET /aplicacoes/{id}/fatias/{codigo},
    // sob Features:P1, com sessão simulada e IPainelDeFatias falso (o GitLab nunca é chamado).
    [Trait("Fatia", "P1")]
    public sealed class FatiasHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class PainelFalso : IPainelDeFatias
        {
            public Dictionary<string, ResultadoDoPainel> Resultados { get; } = [];
            public List<string> Atualizadas { get; } = [];
            public Result Atualizar { get; set; } = Result.Sucesso();
            public int ConsultasDeLista { get; set; }

            public Task<ResultadoDoPainel> ObterAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
                => Task.FromResult(Resultados[aplicacao.Chave]);

            public Task<IReadOnlyList<ResultadoDoPainel>> ObterAsync(IReadOnlyList<AplicacaoDoPainel> aplicacoes, CancellationToken cancellationToken = default)
            {
                ConsultasDeLista++;
                return Task.FromResult<IReadOnlyList<ResultadoDoPainel>>(aplicacoes.Select(a => Resultados[a.Chave]).ToList());
            }

            public Task<ResultadoDoPainel> ObterFrescoAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
                => Task.FromResult(Resultados[aplicacao.Chave]);

            public Task<Result> AtualizarAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
            {
                Atualizadas.Add(aplicacao.Chave);
                return Task.FromResult(Atualizar);
            }
        }

        private static readonly DateTimeOffset Agora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        private readonly InfraestruturaFixture _infra;
        private readonly PainelFalso _painel = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public FatiasHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string? p1) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:P1", p1 ?? string.Empty);
            b.UseSetting("GitLab:Url", "https://gitlab.exemplo.com");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IPainelDeFatias>();
                s.AddSingleton<IPainelDeFatias>(_painel);
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
            WebApplicationFactory<Program> api, HttpMethod metodo, string url, string sessao, int? pagina = null, int? tamanho = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            if (pagina is not null) pedido.Headers.Add("X-Page", pagina.ToString());
            if (tamanho is not null) pedido.Headers.Add("X-Page-Size", tamanho.ToString());
            return cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        // Aplicação com workspace, Projeto de código Api e Frontend, e papéis. Devolve o PublicId.
        private async Task<Guid> AplicacaoAsync(string nome, bool comWorkspace = true, params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupo = $"g{Guid.NewGuid():N}"[..10];
            var app = AplicacaoEntity.Criar(nome, ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), grupo).Value;
            if (comWorkspace)
                app.DefinirWorkspace(ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-workspace");
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-api", $"{nome}-api", Stack.Dotnet));
            ctx.Add(ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-frontend", $"{nome}-frontend", Stack.Angular));
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                ctx.Add(PoliticaSeveridadeEntity.Padrao(app.Id, amb));
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        private static FatiaLida Fatia(string codigo, EstadoDaFatia estado, DetalheDaFatia? detalhe = null) => new(
            codigo, $"Título {codigo}", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, $"specs/feature/x/{codigo}.md", 7,
            new ResultadoDeEstado(estado, null, false, false), false, detalhe);

        private void Programar(Guid app, params FatiaLida[] fatias)
            => _painel.Resultados[app.ToString()] = new ResultadoDoPainel(app.ToString(), SituacaoDoPainel.Ok, fatias, Agora, false, null);

        [Fact, DisplayName("Critério 10: lista só o que a pessoa vê, pagina com X-* e filtra por aba; body é o envelope com itens e avisos")]
        public async Task Lista_Pagina_E_Filtra()
        {
            var sub = $"sub-f-{Guid.NewGuid():N}";
            var visivel = await AplicacaoAsync("visivel", true, (sub, PapelAplicacao.Membro));
            var oculta = await AplicacaoAsync("oculta");
            var semWs = await AplicacaoAsync("semws", false, (sub, PapelAplicacao.Membro));
            Programar(visivel, Fatia("A1", EstadoDaFatia.ProntaParaEntrega), Fatia("A2", EstadoDaFatia.EmRevisao), Fatia("A3", EstadoDaFatia.EmExecucao), Fatia("A4", EstadoDaFatia.Planejada));
            _painel.Resultados[semWs.ToString()] = new ResultadoDoPainel(semWs.ToString(), SituacaoDoPainel.SemWorkspace, [], null, false, null);
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Get, "/api/v1/fatias?aba=todas&sort=codigo&order=desc", sessao, pagina: 2, tamanho: 2);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resposta.Headers.GetValues("X-Total-Count").Single().Should().Be("4");
            resposta.Headers.GetValues("X-Total-Pages").Single().Should().Be("2");
            resposta.Headers.GetValues("X-Page").Single().Should().Be("2");
            resposta.Headers.GetValues("X-Page-Size").Single().Should().Be("2");
            var corpo = await CorpoAsync(resposta);
            corpo.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("codigo").GetString()).Should().Equal("A2", "A1");
            corpo.GetProperty("itens")[0].GetProperty("estado").GetString().Should().Be("em-revisao");
            corpo.GetProperty("avisos").EnumerateArray().Should().ContainSingle(a => a.GetProperty("aplicacaoId").GetGuid() == semWs)
                .Which.GetProperty("codigo").GetString().Should().Be("painel.sem-workspace");
            corpo.GetProperty("aplicacoes").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().NotContain(oculta);
            corpo.TryGetProperty("geradoEm", out _).Should().BeTrue();
        }

        [Fact, DisplayName("Critério 11: MOR vê todas e id inacessível em ?aplicacoes= é ignorado")]
        public async Task Mor_E_Filtro_De_Aplicacoes()
        {
            var a = await AplicacaoAsync("mora");
            var b = await AplicacaoAsync("morb");
            Programar(a, Fatia("A1", EstadoDaFatia.EmRevisao));
            Programar(b, Fatia("B1", EstadoDaFatia.EmRevisao));
            var membro = $"sub-f-{Guid.NewGuid():N}";
            await AplicacaoAsync("so-membro", true, (membro, PapelAplicacao.Membro));

            var mor = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/fatias?aplicacoes={a},{b}", await SessaoAsync(_ligada, "sub-mor-f", "mor"));
            var estranho = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/fatias?aplicacoes={a},{b}", await SessaoAsync(_ligada, membro));

            (await CorpoAsync(mor)).GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("codigo").GetString()).Should().BeEquivalentTo(["A1", "B1"]);
            estranho.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(estranho)).GetProperty("itens").EnumerateArray().Should().BeEmpty();
            estranho.Headers.GetValues("X-Total-Count").Single().Should().Be("0");
        }

        [Fact, DisplayName("Aba e projeto inválidos respondem 400 com code estável")]
        public async Task Parametros_Invalidos()
        {
            var sessao = await SessaoAsync(_ligada, "sub-mor-f2", "mor");

            var aba = await EnviarAsync(_ligada, HttpMethod.Get, "/api/v1/fatias?aba=lixo", sessao);
            var projeto = await EnviarAsync(_ligada, HttpMethod.Get, "/api/v1/fatias?projeto=banco", sessao);

            aba.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CorpoAsync(aba)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("painel.aba-invalida");
            (await CorpoAsync(projeto)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("painel.projeto-invalido");
        }

        [Fact, DisplayName("Atualizar: 204 com papel; 404 sem papel; 429 painel.atualizacao-recente repassado")]
        public async Task Atualizar()
        {
            var sub = $"sub-f-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("atualiza", true, (sub, PapelAplicacao.Membro));
            var url = $"/api/v1/aplicacoes/{app}/fatias/atualizar";

            var ok = await EnviarAsync(_ligada, HttpMethod.Post, url, await SessaoAsync(_ligada, sub));
            var semPapel = await EnviarAsync(_ligada, HttpMethod.Post, url, await SessaoAsync(_ligada, "sub-estranho-f"));
            _painel.Atualizar = Result.Falha(new Erro("painel.atualizacao-recente", "Aguarde.", TipoErro.TooManyRequests));
            var limite = await EnviarAsync(_ligada, HttpMethod.Post, url, await SessaoAsync(_ligada, sub));
            _painel.Atualizar = Result.Sucesso();

            ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
            semPapel.StatusCode.Should().Be(HttpStatusCode.NotFound);
            limite.StatusCode.Should().Be((HttpStatusCode)429);
            (await CorpoAsync(limite)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("painel.atualizacao-recente");
            _painel.Atualizadas.Should().Equal(app.ToString(), app.ToString());
        }

        [Fact, DisplayName("Critério 12: a gaveta devolve os links montados sobre GitLab:Url; sem papel é 404")]
        public async Task Gaveta()
        {
            var sub = $"sub-f-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("gaveta", true, (sub, PapelAplicacao.Membro));
            var detalhe = new DetalheDaFatia("A2 — T", "opened", [], [new TaskDaGaveta("A2", 1, 11, true, false, "0b4bd79", "pai", false, RepositorioFatia.Api)],
                [new MrDaGaveta(RepositorioFatia.Api, 5, "A2 — T", "opened", "feature/a2-t", null)]);
            Programar(app, Fatia("A2", EstadoDaFatia.EmRevisao, detalhe));

            var ok = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/A2", await SessaoAsync(_ligada, sub));
            var semPapel = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/A2", await SessaoAsync(_ligada, "sub-estranho-g"));
            var inexistente = await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/Z9", await SessaoAsync(_ligada, sub));

            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(ok);
            corpo.GetProperty("spec").GetProperty("link").GetString().Should().StartWith("https://gitlab.exemplo.com/").And.EndWith("-workspace/-/blob/main/specs/feature/x/A2.md");
            corpo.GetProperty("tasks")[0].GetProperty("linkCommit").GetString().Should().EndWith("-api/-/commit/0b4bd79");
            corpo.GetProperty("repositorios").EnumerateArray().Select(r => r.GetProperty("repositorio").GetString()).Should().Equal("api", "frontend");
            semPapel.StatusCode.Should().Be(HttpStatusCode.NotFound);
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Critério 18: Features:P1 ausente responde 404 nas três rotas")]
        public async Task FlagDesligada()
        {
            var app = await AplicacaoAsync("flag");
            var sessao = await SessaoAsync(_desligada, "sub-mor-f3", "mor");

            (await EnviarAsync(_desligada, HttpMethod.Get, "/api/v1/fatias", sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await EnviarAsync(_desligada, HttpMethod.Post, $"/api/v1/aplicacoes/{app}/fatias/atualizar", sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await EnviarAsync(_desligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/A1", sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            _painel.ConsultasDeLista.Should().Be(0);
        }

        [Fact, DisplayName("Sem sessão as rotas respondem 401")]
        public async Task SemSessao()
        {
            var cliente = _ligada.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            (await cliente.GetAsync("/api/v1/fatias", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
