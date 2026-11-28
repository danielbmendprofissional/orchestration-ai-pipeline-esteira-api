using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Pipeline
{
    // E2.3: `POST /api/pipeline/v2/resultado` fim a fim (id_token, handler real, PostgreSQL real, schema pelas migrations).
    [Trait("Fatia", "E2")]
    public sealed class ResultadoHttpTests(PipelineApiReal api, InfraestruturaFixture infra) : IClassFixture<PipelineApiReal>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly HttpClient _cliente = api.WithWebHostBuilder(b => b.UseSetting("Features:E1b", "true")).CreateClient();

        private static JsonObject Achado(string severidade, string titulo = "XSS refletido") => new()
        {
            ["severidade"] = severidade, ["titulo"] = titulo, ["regra_id"] = "zap.xss", ["arquivo"] = "src/a.cs", ["linha"] = 3, ["descricao"] = "d"
        };

        private static JsonObject Analise(string destino, string fonte = "zap", string veredito = "reprovado", params JsonObject[] achados) => new()
        {
            ["fonte"] = fonte, ["tipo"] = "ferramenta", ["escopo"] = "deploy", ["destino"] = destino, ["veredito"] = veredito,
            ["achados"] = new JsonArray(achados.Select(a => (JsonNode)a.DeepClone()).ToArray())
        };

        private static JsonObject Corpo(CenarioDePipeline c, string process, string env, string pipeline, params JsonObject[] analises) => new()
        {
            ["process"] = process, ["env"] = env,
            ["entrega_id"] = c.Entrega.PublicId.ToString(), ["fluxo_id"] = c.Fluxo.PublicId.ToString(),
            ["sha"] = CenarioDePipeline.Sha, ["projeto"] = c.Caminho, ["pipeline_id"] = pipeline,
            ["analises"] = new JsonArray(analises.Select(a => (JsonNode)a.DeepClone()).ToArray())
        };

        private async Task<(HttpStatusCode Status, string Texto)> PostAsync(CenarioDePipeline c, string rota, JsonObject corpo)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/pipeline/v2/" + rota)
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json")
            };
            pedido.Headers.TryAddWithoutValidation("Authorization", "Bearer " + api.Emissor.IdToken(c.Caminho));
            var resposta = await _cliente.SendAsync(pedido, Ct);
            return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(Ct));
        }

        private async Task<CenarioDePipeline> CenarioAbertoAsync(string process, string env)
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var fluxo = await ctx.Set<FluxoEntity>().Include(f => f.Etapas).SingleAsync(f => f.Id == c.Fluxo.Id, Ct);
            fluxo.RegistrarSha(CenarioDePipeline.Sha);
            foreach (var e in new[] { EtapaDoFluxo.Qa, EtapaDoFluxo.Hml, EtapaDoFluxo.Prd })
                fluxo.Etapa(e).IniciarExecucao(701, DateTime.UtcNow);
            await ctx.SaveChangesAsync(Ct);
            return c;
        }

        private async Task<EtapaFluxoEntity> EtapaAsync(CenarioDePipeline c, EtapaDoFluxo etapa)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<EtapaFluxoEntity>().AsNoTracking().SingleAsync(e => e.FluxoId == c.Fluxo.Id && e.Etapa == etapa, Ct);
        }

        private async Task<List<AnaliseEntity>> AnalisesAsync(CenarioDePipeline c)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<AnaliseEntity>().AsNoTracking().Include(a => a.Achados).Where(a => a.FluxoId == c.Fluxo.Id).ToListAsync(Ct);
        }

        [Fact, DisplayName("8: excesso em qa responde 200 bloqueado, grava as Analises do Deploy por Destino e leva a Etapa a bloqueada com resultado.bloqueado")]
        public async Task Qa_BloqueiaEtapaMasResponde200()
        {
            var c = await CenarioAbertoAsync("fluxo", "qa");

            var (status, texto) = await PostAsync(c, "resultado", Corpo(c, "fluxo", "qa", "701",
                Analise("k8s-a", "zap", "reprovado", Achado("high", "titulo-secreto-de-terceiro")), Analise("k8s-b", "smoke", "aprovado")));

            status.Should().Be(HttpStatusCode.OK, texto);
            JsonNode.Parse(texto)!["decisao"]!.GetValue<string>().Should().Be("bloqueado");
            texto.Should().NotContain("titulo-secreto-de-terceiro");
            var analises = await AnalisesAsync(c);
            analises.Select(a => a.DestinoNome).Should().BeEquivalentTo(["k8s-a", "k8s-b"]);
            analises.Should().OnlyContain(a => a.Escopo == EscopoAnalise.Deploy);
            var etapa = await EtapaAsync(c, EtapaDoFluxo.Qa);
            etapa.Estado.Should().Be(EstadoEtapa.Bloqueada);
            etapa.CodigoFalha.Should().Be("resultado.bloqueado");
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var decisao = await ctx.Set<DecisaoDoGateEntity>().SingleAsync(d => d.FluxoId == c.Fluxo.Id, Ct);
            (decisao.Momento, decisao.Decisao).Should().Be((MomentoDoGate.Resultado, DecisaoGate.Bloqueado));
        }

        [Fact, DisplayName("8: excesso em production responde 200 avisou e a Etapa nao bloqueia")]
        public async Task Producao_Avisou()
        {
            var c = await CenarioAbertoAsync("promocao", "production");

            var (status, texto) = await PostAsync(c, "resultado", Corpo(c, "promocao", "production", "701", Analise("k8s-a", "zap", "reprovado", Achado("critical"))));

            status.Should().Be(HttpStatusCode.OK, texto);
            JsonNode.Parse(texto)!["decisao"]!.GetValue<string>().Should().Be("avisou");
            (await EtapaAsync(c, EtapaDoFluxo.Prd)).Estado.Should().Be(EstadoEtapa.Executando);
        }

        [Fact, DisplayName("14: Achado com HTML e a marcacao volta como texto puro no banco; o corpo de erro nao repete titulo nem descricao")]
        public async Task TextoDeTerceiro_Puro()
        {
            var c = await CenarioAbertoAsync("fluxo", "qa");
            var achado = Achado("low", "<script>alert(1)</script>");

            (await PostAsync(c, "resultado", Corpo(c, "fluxo", "qa", "701", Analise("k8s-a", "zap", "reprovado", achado)))).Status.Should().Be(HttpStatusCode.OK);

            (await AnalisesAsync(c)).Single().Achados.Single().Titulo.Should().Be("<script>alert(1)</script>");
            var (status, texto) = await PostAsync(c, "resultado", Corpo(c, "fluxo", "qa", "701",
                new JsonObject { ["fonte"] = "zap", ["tipo"] = "ferramenta", ["escopo"] = "artefato", ["veredito"] = "aprovado", ["achados"] = new JsonArray(achado.DeepClone()) }));
            status.Should().Be((HttpStatusCode)422);
            texto.Should().NotContain("<script>");
        }

        [Fact, DisplayName("7: dois resultados identicos simultaneos terminam sem 500 e com uma Analise por chave")]
        public async Task Simultaneos_SemErro500()
        {
            var c = await CenarioAbertoAsync("fluxo", "qa");
            var corpo = Corpo(c, "fluxo", "qa", "701", Analise("k8s-a", "zap", "reprovado", Achado("high")));

            var respostas = await Task.WhenAll(PostAsync(c, "resultado", corpo), PostAsync(c, "resultado", corpo));

            respostas.Should().OnlyContain(r => r.Status == HttpStatusCode.OK, string.Join(" | ", respostas.Select(r => r.Texto)));
            (await AnalisesAsync(c)).Should().ContainSingle();
            (await EtapaAsync(c, EtapaDoFluxo.Qa)).Estado.Should().Be(EstadoEtapa.Bloqueada);
        }

        [Fact, DisplayName("8: sem Analises responde 422 gate.sem-analises e escopo artefato responde 422 pipeline.corpo-invalido, sem gravar")]
        public async Task Recusas()
        {
            var c = await CenarioAbertoAsync("fluxo", "qa");

            var (s1, t1) = await PostAsync(c, "resultado", Corpo(c, "fluxo", "qa", "701"));
            s1.Should().Be((HttpStatusCode)422);
            JsonNode.Parse(t1)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("gate.sem-analises");

            var (s2, t2) = await PostAsync(c, "resultado", Corpo(c, "fluxo", "qa", "701",
                new JsonObject { ["fonte"] = "zap", ["tipo"] = "ferramenta", ["escopo"] = "artefato", ["veredito"] = "aprovado", ["achados"] = new JsonArray() }));
            s2.Should().Be((HttpStatusCode)422);
            JsonNode.Parse(t2)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.corpo-invalido");
            (await AnalisesAsync(c)).Should().BeEmpty();
        }
    }
}
