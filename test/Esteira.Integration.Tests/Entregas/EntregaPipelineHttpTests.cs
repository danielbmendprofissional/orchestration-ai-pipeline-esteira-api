using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Esteira.Integration.Tests.Pipeline;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Entregas
{
    // E1b.4 (spec 02-tela-da-entrega.md, Testes -> API/HTTP): esteira-abrir e webhook pela borda HTTP, leitura pela rota da tela.
    // A API de verdade com o handler real, PostgreSQL real e o id_token do GitLab falso.
    [Trait("Fatia", "E1b")]
    public sealed class EntregaPipelineHttpTests : IClassFixture<PipelineApiReal>
    {
        private const string Segredo = "segredo-do-webhook-e1b-5d2c";
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _api;
        private readonly PipelineApiReal _base;

        public EntregaPipelineHttpTests(PipelineApiReal api, InfraestruturaFixture infra)
        {
            _base = api;
            _infra = infra;
            _api = api.WithWebHostBuilder(b =>
            {
                b.UseSetting("Features:E1b", "true");
                b.UseSetting("GitLab:Webhook:Secret", Segredo);
            });
        }

        private async Task<HttpResponseMessage> AbrirAsync(CenarioDePipeline c, string process, string env, string pipelineId,
            string por = "", string motivo = "")
        {
            var corpo = new JsonObject
            {
                ["process"] = process,
                ["env"] = env,
                ["entrega_id"] = c.Entrega.PublicId.ToString(),
                ["fluxo_id"] = c.Fluxo.PublicId.ToString(),
                ["sha"] = CenarioDePipeline.Sha,
                ["projeto"] = c.Caminho,
                ["pipeline_id"] = pipelineId,
                ["versao"] = "",
                ["atalho_por"] = por,
                ["atalho_motivo"] = motivo,
                ["flag_somente_leitura"] = false
            };
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/pipeline/v2/abrir")
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json")
            };
            pedido.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _base.Emissor.IdToken(c.Caminho));
            return await _api.CreateClient().SendAsync(pedido, Ct);
        }

        private async Task<HttpResponseMessage> WebhookAsync(CenarioDePipeline c, string pipelineId, string status)
        {
            var fim = DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            var corpo = $$$"""{"object_attributes":{"id":{{{pipelineId}}},"status":"{{{status}}}","ref":"main","sha":"{{{CenarioDePipeline.Sha}}}","finished_at":"{{{fim}}}"},"project":{"path_with_namespace":"{{{c.Caminho}}}"}}""";
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/gitlab")
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            };
            pedido.Headers.Add("X-Gitlab-Token", Segredo);
            pedido.Headers.Add("X-Gitlab-Event", "Pipeline Hook");
            return await _api.CreateClient().SendAsync(pedido, Ct);
        }

        private async Task<(HttpStatusCode Status, string Texto, JsonNode? Corpo)> EntregaAsync(CenarioDePipeline c)
        {
            var sub = $"sub-mor-{Guid.NewGuid():N}";
            var tokens = ProvedorOidcFalso.Tokens("n", sub, ["mor"]);
            var sessao = Guid.NewGuid().ToString("N");
            await _api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(sessao, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/entregas/{c.Entrega.PublicId}");
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            var r = await _api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false })
                .SendAsync(pedido, Ct);
            var texto = await r.Content.ReadAsStringAsync(Ct);
            return (r.StatusCode, texto, r.IsSuccessStatusCode ? JsonNode.Parse(texto) : null);
        }

        private static JsonNode Etapa(JsonNode entrega, string nome)
            => entrega["fluxos"]![0]!["etapas"]!.AsArray().Single(e => e!["etapa"]!.GetValue<string>() == nome)!;

        [Fact, DisplayName("2/5/12: esteira-abrir poe a Etapa qa em executando e o webhook success a leva a aguardando-aprovacao, visivel na rota da Entrega")]
        public async Task AbrirEWebhook_AteAguardandoAprovacao()
        {
            var c = await CenarioDePipeline.CriarAsync(_infra);

            (await AbrirAsync(c, "fluxo", "qa", "7001")).StatusCode.Should().Be(HttpStatusCode.OK);
            var depoisDoAbrir = await EntregaAsync(c);
            depoisDoAbrir.Status.Should().Be(HttpStatusCode.OK);
            Etapa(depoisDoAbrir.Corpo!, "qa")["estado"]!.GetValue<string>().Should().Be("executando");

            (await WebhookAsync(c, "7001", "success")).StatusCode.Should().Be(HttpStatusCode.OK);

            var depoisDoWebhook = await EntregaAsync(c);
            Etapa(depoisDoWebhook.Corpo!, "qa")["estado"]!.GetValue<string>().Should().Be("aguardando-aprovacao");
            Etapa(depoisDoWebhook.Corpo!, "hml")["estado"]!.GetValue<string>().Should().Be("nao-iniciado");
        }

        [Theory, DisplayName("4: atalho_por acima de 200 ou atalho_motivo acima de 1000 responde 422 pipeline.corpo-invalido e nada e gravado")]
        [InlineData(201, 5)]
        [InlineData(5, 1001)]
        public async Task AtalhoAcimaDoLimite_422(int tamanhoPor, int tamanhoMotivo)
        {
            var c = await CenarioDePipeline.CriarAsync(_infra);

            var r = await AbrirAsync(c, "fluxo", "production", "7002", new string('a', tamanhoPor), new string('b', tamanhoMotivo));

            r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            JsonNode.Parse(await r.Content.ReadAsStringAsync(Ct))!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.corpo-invalido");
            var entrega = await EntregaAsync(c);
            Etapa(entrega.Corpo!, "prd")["estado"]!.GetValue<string>().Should().Be("nao-iniciado");
            Etapa(entrega.Corpo!, "qa")["estado"]!.GetValue<string>().Should().Be("nao-iniciado");
        }

        [Fact, DisplayName("12: pipelineUrl e montada com GitLab:Url, o caminho do Projeto e o id da pipeline, sem segredo nem credencial")]
        public async Task PipelineUrl_MontadaSemSegredo()
        {
            var c = await CenarioDePipeline.CriarAsync(_infra);
            (await AbrirAsync(c, "fluxo", "qa", "7003")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await WebhookAsync(c, "7003", "success")).StatusCode.Should().Be(HttpStatusCode.OK);

            var entrega = await EntregaAsync(c);

            var url = Etapa(entrega.Corpo!, "qa")["pipelineUrl"]!.GetValue<string>();
            url.Should().Be($"{EmissorJwksFalso.Url}/{c.Caminho}/-/pipelines/7003");
            var uri = new Uri(url);
            uri.UserInfo.Should().BeEmpty();
            uri.Query.Should().BeEmpty();
            entrega.Texto.Should().NotContain(Segredo).And.NotContain("segredo-de-teste");
        }
    }
}
