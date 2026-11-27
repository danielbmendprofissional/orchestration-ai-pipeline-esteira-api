using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Pipeline
{
    // A API de verdade, com o handler real da E1.4 (sem o espião de IAbrirPipeline) e o GitLab falso só como emissor do id_token.
    public sealed class PipelineApiReal(InfraestruturaFixture infra) : WebApplicationFactory<Program>
    {
        internal EmissorJwksFalso Emissor { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
            builder.UseSetting("Keycloak:ClientId", "esteira-api");
            builder.UseSetting("Keycloak:ClientSecret", "segredo-de-teste");
            builder.UseSetting("Autenticacao:RequerHttps", "false");
            builder.UseSetting("GitLab:Url", EmissorJwksFalso.Url);

            foreach (var (chave, valor) in infra.Configuracao("t" + Guid.NewGuid().ToString("N")[..11]))
                builder.UseSetting(chave, valor);

            builder.ConfigureTestServices(services =>
                services.Configure<JwtBearerOptions>(PipelineApiDeTeste.PipelineEsquema, o => o.BackchannelHttpHandler = Emissor.Handler));
        }
    }

    // E1.4, critérios 4 e 15 fim a fim: id_token, rota, handler real, PostgreSQL real.
    [Trait("Fatia", "E1")]
    public sealed class AbrirPipelineFimAFimTests(PipelineApiReal api, InfraestruturaFixture infra) : IClassFixture<PipelineApiReal>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<(HttpStatusCode Status, JsonNode Corpo)> PostAsync(CenarioDePipeline c, string process, string env, Guid? fluxo = null)
        {
            var corpo = new JsonObject
            {
                ["process"] = process,
                ["env"] = env,
                ["entrega_id"] = c.Entrega.PublicId.ToString(),
                ["fluxo_id"] = (fluxo ?? c.Fluxo.PublicId).ToString(),
                ["sha"] = CenarioDePipeline.Sha,
                ["projeto"] = c.Caminho,
                ["pipeline_id"] = "1",
                ["versao"] = "",
                ["atalho_por"] = "",
                ["atalho_motivo"] = "",
                ["flag_somente_leitura"] = false
            };
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/pipeline/v2/abrir")
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json")
            };
            pedido.Headers.TryAddWithoutValidation("Authorization", "Bearer " + api.Emissor.IdToken(c.Caminho));
            var resposta = await api.CreateClient().SendAsync(pedido, Ct);
            return (resposta.StatusCode, JsonNode.Parse(await resposta.Content.ReadAsStringAsync(Ct))!);
        }

        [Fact, DisplayName("4/15: fluxo em qa de Entrega aberta responde 200 com feature_flags e destinos por Ordem, e registra o SHA")]
        public async Task CaminhoFeliz()
        {
            var c = await CenarioDePipeline.CriarAsync(infra, true,
                (AmbienteDeploy.Qa, "b", 2, new() { ["APP_URL"] = "https://qa" }),
                (AmbienteDeploy.Qa, "a", 1, new() { ["K8S_NAMESPACE"] = "qa" }));

            var (status, corpo) = await PostAsync(c, "fluxo", "qa");

            status.Should().Be(HttpStatusCode.OK);
            corpo["feature_flags"]!.GetValue<string>().Should().Be("B2=true");
            corpo["destinos"]!.AsArray().Select(d => d!["nome"]!.GetValue<string>()).Should().Equal("a", "b");
            corpo["destinos"]![0]!["ordem"]!.GetValue<int>().Should().Be(1);
            corpo["destinos"]![0]!["configuracao"]!["K8S_NAMESPACE"]!.GetValue<string>().Should().Be("qa");

            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await ctx.Set<Esteira.Domain.Entregas.FluxoEntity>().FindAsync([c.Fluxo.Id], Ct))!.Sha.Should().Be(CenarioDePipeline.Sha);
        }

        [Fact, DisplayName("7/8: erros saem com o status do contrato: fluxo desconhecido 409, processo não suportado 422")]
        public async Task ErrosComStatusDoContrato()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);

            var semEntrega = await PostAsync(c, "fluxo", "qa", Guid.NewGuid());
            semEntrega.Status.Should().Be(HttpStatusCode.Conflict);
            semEntrega.Corpo["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.sem-entrega-aberta");

            var naoSuportado = await PostAsync(c, "liberacao", "production");
            naoSuportado.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            naoSuportado.Corpo["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.processo-nao-suportado");
        }
    }
}
