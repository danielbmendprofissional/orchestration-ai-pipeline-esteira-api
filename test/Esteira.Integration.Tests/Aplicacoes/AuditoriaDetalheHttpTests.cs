using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // X1.4, critérios 5, 8, 9 e 11: detalhe da trilha, 404 idêntico, segredo mascarado e nenhuma rota de escrita.
    [Trait("Fatia", "X1")]
    public sealed class AuditoriaDetalheHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;

        public AuditoriaDetalheHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => b.UseSetting("Features:X1", "true"));
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<string> SessaoAsync(string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await _ligada.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static string SubUnico(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}"[..20];

        private async Task<AplicacaoEntity> AplicacaoAsync(params string[] subsComPapel)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var sub in subsComPapel)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return app;
        }

        private async Task<Guid> RegistroAsync(long? aplicacaoId, AcaoAuditoria acao, string? antes, string? depois, string? motivo = null)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var registro = RegistroAuditoriaEntity.Criar(DateTime.UtcNow, "sub-x", "Destino", Guid.CreateVersion7(), acao, antes, depois,
                CategoriaAuditoria.Cadastro, OrigemAuditoria.Tela, aplicacaoId, "Fulana", null, "DST", null, motivo);
            ctx.Add(registro);
            await ctx.SaveChangesAsync(Ct);
            return registro.PublicId;
        }

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, string? sessao)
        {
            var cliente = _ligada.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            return await cliente.SendAsync(pedido, Ct);
        }

        [Fact, DisplayName("5: registro fora do escopo e registro inexistente respondem 404 com corpo idêntico")]
        public async Task ForaDoEscopo_E_Inexistente_404Identico()
        {
            var sub = SubUnico("membro");
            var alheia = await AplicacaoAsync();
            var alheio = await RegistroAsync(alheia.Id, AcaoAuditoria.Criou, null, """{"nome":"x"}""");
            var semApp = await RegistroAsync(null, AcaoAuditoria.Criou, null, """{"nome":"x"}""");
            var sessao = await SessaoAsync(sub);

            var fora = await EnviarAsync(HttpMethod.Get, $"/api/v1/auditoria/{alheio}", sessao);
            var foraSemApp = await EnviarAsync(HttpMethod.Get, $"/api/v1/auditoria/{semApp}", sessao);
            var inexistente = await EnviarAsync(HttpMethod.Get, $"/api/v1/auditoria/{Guid.NewGuid()}", sessao);

            fora.StatusCode.Should().Be(HttpStatusCode.NotFound);
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
            foraSemApp.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var corpo = await inexistente.Content.ReadAsStringAsync(Ct);
            (await fora.Content.ReadAsStringAsync(Ct)).Should().Be(corpo);
            (await foraSemApp.Content.ReadAsStringAsync(Ct)).Should().Be(corpo);
            corpo.Should().NotBeNullOrWhiteSpace();
        }

        [Fact, DisplayName("8 e 9: detalhe devolve mudanças só do que mudou, motivo, e mascara segredo")]
        public async Task Detalhe_Mudancas_Motivo_Mascara()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync(sub);
            var id = await RegistroAsync(app.Id, AcaoAuditoria.Alterou,
                """{"nome":"A","replicas":1,"password":"velha-senha"}""",
                """{"nome":"A","replicas":2,"password":"nova-senha"}""", "porque sim");
            var sessao = await SessaoAsync(sub);

            var r = await EnviarAsync(HttpMethod.Get, $"/api/v1/auditoria/{id}", sessao);

            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var texto = await r.Content.ReadAsStringAsync(Ct);
            texto.Should().NotContain("velha-senha").And.NotContain("nova-senha");
            var json = JsonDocument.Parse(texto).RootElement;
            json.GetProperty("id").GetGuid().Should().Be(id);
            json.GetProperty("motivo").GetString().Should().Be("porque sim");
            json.GetProperty("categoria").GetString().Should().Be("cadastro");
            json.GetProperty("aplicacao").GetProperty("id").GetGuid().Should().Be(app.PublicId);
            var mudancas = json.GetProperty("mudancas").EnumerateArray()
                .Select(m => (m.GetProperty("campo").GetString(), m.GetProperty("antes").GetString(), m.GetProperty("depois").GetString())).ToArray();
            mudancas.Should().BeEquivalentTo(new[] { ("replicas", "1", "2"), ("password", "••••", "••••") });
        }

        [Fact, DisplayName("9: a busca por texto não acha campo de segredo, mas acha campo comum")]
        public async Task Busca_NaoAchaSegredo()
        {
            var sub = SubUnico("membro");
            var app = await AplicacaoAsync(sub);
            await RegistroAsync(app.Id, AcaoAuditoria.Criou, null, """{"clientSecret":"abc","replicasx":2}""");
            var sessao = await SessaoAsync(sub);

            async Task<int> Achados(string busca)
            {
                var r = await EnviarAsync(HttpMethod.Get, $"/api/v1/auditoria?busca={busca}", sessao);
                r.StatusCode.Should().Be(HttpStatusCode.OK);
                return JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement.GetArrayLength();
            }

            (await Achados("replicasx")).Should().Be(1);
            (await Achados("clientSecret")).Should().Be(0);
            (await Achados("secret")).Should().Be(0);
        }

        [Theory, DisplayName("11: não existe rota de escrita em /auditoria (404 ou 405)")]
        [InlineData("POST", "/api/v1/auditoria")]
        [InlineData("PUT", "/api/v1/auditoria")]
        [InlineData("PATCH", "/api/v1/auditoria")]
        [InlineData("DELETE", "/api/v1/auditoria")]
        [InlineData("POST", "/api/v1/auditoria/00000000-0000-0000-0000-000000000001")]
        [InlineData("PUT", "/api/v1/auditoria/00000000-0000-0000-0000-000000000001")]
        [InlineData("PATCH", "/api/v1/auditoria/00000000-0000-0000-0000-000000000001")]
        [InlineData("DELETE", "/api/v1/auditoria/00000000-0000-0000-0000-000000000001")]
        [InlineData("DELETE", "/api/v1/auditoria/categorias")]
        [InlineData("POST", "/api/v1/auditoria/filtros")]
        public async Task SemRotaDeEscrita(string metodo, string url)
        {
            var sessao = await SessaoAsync(SubUnico("mor"), "mor");

            var r = await EnviarAsync(new HttpMethod(metodo), url, sessao);

            r.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        [Fact, DisplayName("OpenAPI descreve as quatro rotas de leitura da auditoria e o schema do detalhe")]
        public async Task OpenApi_QuatroRotas()
        {
            var doc = JsonNode.Parse(await _ligada.CreateClient().GetStringAsync("/openapi/v1.json", Ct))!;
            var paths = doc["paths"]!.AsObject();

            foreach (var rota in new[] { "/api/v1/auditoria", "/api/v1/auditoria/categorias", "/api/v1/auditoria/filtros", "/api/v1/auditoria/{id}" })
                paths[rota]!["get"]!["responses"]!.AsObject().ContainsKey("200").Should().BeTrue(rota);
            paths["/api/v1/auditoria/{id}"]!["get"]!["responses"]!["404"]!["content"]!["application/json"]!["schema"]!["$ref"]!
                .GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
            paths.Select(p => p.Key).Where(k => k.StartsWith("/api/v1/auditoria"))
                .Should().OnlyContain(k => paths[k]!.AsObject().Select(m => m.Key).All(m => m == "get" || m == "parameters"));
            doc["components"]!["schemas"]!["RegistroAuditoriaDetalheDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().Contain(["id", "mudancas", "motivo"]);
            doc["components"]!["schemas"]!["MudancaDto"]!["properties"]!.AsObject().Select(p => p.Key)
                .Should().BeEquivalentTo("campo", "antes", "depois");
        }
    }
}
