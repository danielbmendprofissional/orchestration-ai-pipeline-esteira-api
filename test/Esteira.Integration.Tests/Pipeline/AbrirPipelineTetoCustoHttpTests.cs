using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;

namespace Esteira.Integration.Tests.Pipeline
{
    // X3.3, critérios 8 e 9 ponta a ponta: id_token, rota, handler real, PostgreSQL real e o documento OpenAPI pipeline-v2.
    [Trait("Fatia", "X3")]
    public sealed class AbrirPipelineTetoCustoHttpTests(PipelineApiReal api, InfraestruturaFixture infra) : IClassFixture<PipelineApiReal>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<(HttpStatusCode Status, JsonNode Corpo)> PostAsync(CenarioDePipeline c, string process, string env)
        {
            var corpo = new JsonObject
            {
                ["process"] = process,
                ["env"] = env,
                ["entrega_id"] = c.Entrega.PublicId.ToString(),
                ["fluxo_id"] = c.Fluxo.PublicId.ToString(),
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

        [Fact, DisplayName("[X3] 8: o 200 do esteira-abrir traz teto_custo_usd com o teto definido na Aplicação")]
        public async Task TetoDefinido_VaiNaResposta()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var app = await ctx.Set<AplicacaoEntity>().FindAsync([c.Aplicacao.Id], Ct);
                app!.DefinirTetoCusto(7.50m).IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync(Ct);
            }

            var (status, corpo) = await PostAsync(c, "fluxo", "qa");

            status.Should().Be(HttpStatusCode.OK);
            corpo["teto_custo_usd"]!.GetValue<decimal>().Should().Be(7.50m);
        }

        [Fact, DisplayName("[X3] 8: sem teto definido, o 200 do esteira-abrir traz o padrão da Esteira (2.00), nunca nulo")]
        public async Task SemTeto_VaiOPadrao()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);

            var (status, corpo) = await PostAsync(c, "fluxo", "qa");

            status.Should().Be(HttpStatusCode.OK);
            corpo["teto_custo_usd"]!.GetValue<decimal>().Should().Be(2.00m);
        }

        [Fact, DisplayName("[X3] 9: recusa 4xx do esteira-abrir não traz teto_custo_usd")]
        public async Task Recusa_SemTeto()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);

            var (status, corpo) = await PostAsync(c, "liberacao", "production");

            status.Should().Be(HttpStatusCode.UnprocessableEntity);
            corpo["teto_custo_usd"].Should().BeNull();
        }

        [Fact, DisplayName("[X3] 8: o documento OpenAPI pipeline-v2 publica teto_custo_usd na resposta do abrir")]
        public async Task OpenApi_PublicaOTeto()
        {
            var json = await api.CreateClient().GetStringAsync("/openapi/pipeline-v2.json", Ct);

            json.Should().Contain("teto_custo_usd");
        }
    }
}
