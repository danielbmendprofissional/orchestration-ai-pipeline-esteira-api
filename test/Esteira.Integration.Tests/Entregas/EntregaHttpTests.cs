using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Entregas
{
    // E1b.4, critérios 11, 12 e 13: GET /api/v1/entregas/{id} com sessão, PostgreSQL real.
    [Trait("Fatia", "E1b")]
    public sealed class EntregaHttpTests : IClassFixture<ApiDeTeste>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public EntregaHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => { b.UseSetting("Features:E1b", "true"); b.UseSetting("GitLab:Url", "https://gitlab.exemplo.com"); });
            _desligada = api.WithWebHostBuilder(b => b.UseSetting("Features:E1b", ""));
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

        private static Task<HttpResponseMessage> GetAsync(WebApplicationFactory<Program> api, string url, string? sessao)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            return cliente.SendAsync(pedido, Ct);
        }

        private async Task<(CenarioDeEntrega Cenario, string Sub)> CenarioAsync()
        {
            var c = await CenarioDeEntrega.CriarAsync(_infra);
            var sub = $"sub-e-{Guid.NewGuid():N}";
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            ctx.Add(PapelAplicacaoEntity.Criar(c.Aplicacao.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return (c, sub);
        }

        private static async Task<string> CodigoAsync(HttpResponseMessage r)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;
        }

        [Fact, DisplayName("Critério 11: Features:E1b desligado responde 404 de rota, sem o erro da Entrega")]
        public async Task FlagDesligada()
        {
            var (c, sub) = await CenarioAsync();

            var r = await GetAsync(_desligada, $"/api/v1/entregas/{c.Entrega.PublicId}", await SessaoAsync(_desligada, sub));

            r.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await r.Content.ReadAsStringAsync(Ct)).Should().NotContain("entrega.nao-encontrada");
        }

        [Fact, DisplayName("Critério 12: pessoa com papel recebe a Entrega com Fluxos, Etapas e estado")]
        public async Task ComPapel()
        {
            var (c, sub) = await CenarioAsync();

            var r = await GetAsync(_ligada, $"/api/v1/entregas/{c.Entrega.PublicId}", await SessaoAsync(_ligada, sub));

            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = JsonNode.Parse(await r.Content.ReadAsStringAsync(Ct))!;
            corpo["id"]!.GetValue<Guid>().Should().Be(c.Entrega.PublicId);
            corpo["aplicacao"]!["id"]!.GetValue<Guid>().Should().Be(c.Aplicacao.PublicId);
            corpo["abertaPor"]!["nome"]!.GetValue<string>().Should().Be("Fulano");
            corpo["estado"]!.GetValue<string>().Should().Be("em-andamento");
            var fluxo = corpo["fluxos"]!.AsArray().Should().ContainSingle().Subject!;
            fluxo["projeto"]!["nome"]!.GetValue<string>().Should().Be("p0");
            fluxo["tipoMudanca"]!.GetValue<string>().Should().Be("minor");
            fluxo["fatias"]!.AsArray().Select(f => f!["codigo"]!.GetValue<string>()).Should().BeEquivalentTo(["B2", "B3"]);
            fluxo["etapas"]!.AsArray().Select(e => e!["etapa"]!.GetValue<string>()).Should().Equal("abertura", "qa", "hml", "prd");
        }

        [Fact, DisplayName("Critério 12: MOR sem papel recebe a Entrega")]
        public async Task Mor()
        {
            var (c, _) = await CenarioAsync();

            var r = await GetAsync(_ligada, $"/api/v1/entregas/{c.Entrega.PublicId}", await SessaoAsync(_ligada, $"sub-mor-{Guid.NewGuid():N}", "mor"));

            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact, DisplayName("Critério 12: sem sessão responde 401")]
        public async Task SemSessao()
        {
            var (c, _) = await CenarioAsync();

            (await GetAsync(_ligada, $"/api/v1/entregas/{c.Entrega.PublicId}", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact, DisplayName("Critério 13: Entrega inexistente e de outra Aplicação respondem o mesmo 404 entrega.nao-encontrada")]
        public async Task NaoEncontradaUniforme()
        {
            var (c, _) = await CenarioAsync();
            var (_, subOutra) = await CenarioAsync();
            var sessao = await SessaoAsync(_ligada, subOutra);

            var alheia = await GetAsync(_ligada, $"/api/v1/entregas/{c.Entrega.PublicId}", sessao);
            var inexistente = await GetAsync(_ligada, $"/api/v1/entregas/{Guid.NewGuid()}", sessao);

            alheia.StatusCode.Should().Be(HttpStatusCode.NotFound);
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoAsync(alheia)).Should().Be("entrega.nao-encontrada");
            (await CodigoAsync(inexistente)).Should().Be("entrega.nao-encontrada");
        }

        [Fact, DisplayName("O OpenAPI descreve GET /entregas/{id} com 200, 401 e 404 e os campos da Entrega")]
        public async Task OpenApi()
        {
            var doc = JsonNode.Parse(await _ligada.CreateClient().GetStringAsync("/openapi/v1.json", Ct))!;

            var respostas = doc["paths"]!["/api/v1/entregas/{id}"]!["get"]!["responses"]!.AsObject();
            respostas.ContainsKey("200").Should().BeTrue();
            foreach (var status in new[] { "401", "404" })
                respostas[status]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
            var schemas = doc["components"]!["schemas"]!.AsObject();
            schemas["EntregaDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().BeEquivalentTo(["id", "aplicacao", "abertaPor", "abertaEm", "estado", "vezDe", "fluxos"]);
            schemas["EtapaDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().BeEquivalentTo(["etapa", "estado", "codigoFalha", "pulada", "pipelineUrl", "atualizadaEm"]);
        }
    }
}
