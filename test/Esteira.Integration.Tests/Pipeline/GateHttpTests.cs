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
    // E2.2: `POST /api/pipeline/v2/gate` fim a fim (id_token, handler real, PostgreSQL real, schema pelas migrations).
    [Trait("Fatia", "E2")]
    public sealed class GateHttpTests(PipelineApiReal api, InfraestruturaFixture infra) : IClassFixture<PipelineApiReal>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        // Etapas ligadas (Features:E1b): o bloqueio da Etapa só acontece com ela.
        private readonly HttpClient _cliente = api.WithWebHostBuilder(b => b.UseSetting("Features:E1b", "true")).CreateClient();

        private static JsonObject Achado(string severidade, string titulo = "SQL injection") => new()
        {
            ["severidade"] = severidade, ["titulo"] = titulo, ["regra_id"] = "csharp.sqli", ["arquivo"] = "src/Repo.cs", ["linha"] = 42, ["descricao"] = "d"
        };

        private static JsonObject Analise(string fonte, string veredito = "aprovado", string tipo = "ferramenta", params JsonObject[] achados) => new()
        {
            ["fonte"] = fonte, ["tipo"] = tipo, ["escopo"] = "artefato", ["veredito"] = veredito,
            ["achados"] = new JsonArray(achados.Select(a => (JsonNode)a.DeepClone()).ToArray())
        };

        private static JsonObject Corpo(CenarioDePipeline c, string pipeline, params JsonObject[] analises) => new()
        {
            ["process"] = "fluxo", ["env"] = "qa",
            ["entrega_id"] = c.Entrega.PublicId.ToString(), ["fluxo_id"] = c.Fluxo.PublicId.ToString(),
            ["sha"] = CenarioDePipeline.Sha, ["projeto"] = c.Caminho, ["pipeline_id"] = pipeline,
            ["analises"] = new JsonArray(analises.Select(a => (JsonNode)a.DeepClone()).ToArray())
        };

        private async Task<(HttpStatusCode Status, string Texto)> PostAsync(CenarioDePipeline c, string rota, JsonObject corpo, bool comToken = true)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/pipeline/v2/" + rota)
            {
                Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json")
            };
            if (comToken)
                pedido.Headers.TryAddWithoutValidation("Authorization", "Bearer " + api.Emissor.IdToken(c.Caminho));
            var resposta = await _cliente.SendAsync(pedido, Ct);
            return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(Ct));
        }

        // O esteira-abrir registra o SHA e poe a Etapa em executando, como a pipeline real faz antes do gate.
        private async Task<CenarioDePipeline> CenarioAbertoAsync()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            var abrir = new JsonObject
            {
                ["process"] = "fluxo", ["env"] = "qa", ["entrega_id"] = c.Entrega.PublicId.ToString(), ["fluxo_id"] = c.Fluxo.PublicId.ToString(),
                ["sha"] = CenarioDePipeline.Sha, ["projeto"] = c.Caminho, ["pipeline_id"] = "701"
            };
            (await PostAsync(c, "abrir", abrir)).Status.Should().Be(HttpStatusCode.OK);
            return c;
        }

        private async Task<List<AnaliseEntity>> AnalisesAsync(CenarioDePipeline c)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<AnaliseEntity>().AsNoTracking().Include(a => a.Achados).Where(a => a.FluxoId == c.Fluxo.Id).ToListAsync(Ct);
        }

        private async Task<EstadoEtapa> EstadoDaEtapaAsync(CenarioDePipeline c, EtapaDoFluxo etapa)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return (await ctx.Set<EtapaFluxoEntity>().AsNoTracking().SingleAsync(e => e.FluxoId == c.Fluxo.Id && e.Etapa == etapa, Ct)).Estado;
        }

        [Fact, DisplayName("1: Achados dentro da Politica gravam Analises, Achados e decisao e respondem 200 liberado com a contagem")]
        public async Task Liberado()
        {
            var c = await CenarioAbertoAsync();

            var (status, texto) = await PostAsync(c, "gate", Corpo(c, "701", Analise("opengrep", "reprovado", "ferramenta", Achado("medium"), Achado("low"), Achado("info"))));

            status.Should().Be(HttpStatusCode.OK, texto);
            var json = JsonNode.Parse(texto)!;
            json["decisao"]!.GetValue<string>().Should().Be("liberado");
            json["contagem"]!["media"]!.GetValue<int>().Should().Be(1);
            json["contagem"]!["baixa"]!.GetValue<int>().Should().Be(1);
            var analises = await AnalisesAsync(c);
            analises.Should().ContainSingle().Which.Achados.Should().HaveCount(3);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await ctx.Set<DecisaoDoGateEntity>().SingleAsync(d => d.FluxoId == c.Fluxo.Id, Ct)).Decisao.Should().Be(DecisaoGate.Liberado);
            (await EstadoDaEtapaAsync(c, EtapaDoFluxo.Qa)).Should().Be(EstadoEtapa.Executando);
        }

        [Fact, DisplayName("2: Achado alto grava Analises e decisao, bloqueia a Etapa e responde 409 gate.bloqueado sem repetir o texto do Achado")]
        public async Task Bloqueado()
        {
            var c = await CenarioAbertoAsync();

            var (status, texto) = await PostAsync(c, "gate", Corpo(c, "701", Analise("opengrep", "reprovado", "ferramenta", Achado("high", "titulo-secreto-de-terceiro"))));

            status.Should().Be(HttpStatusCode.Conflict);
            JsonNode.Parse(texto)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("gate.bloqueado");
            texto.Should().NotContain("titulo-secreto-de-terceiro").And.NotContain("id_token");
            (await AnalisesAsync(c)).Should().ContainSingle();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var decisao = await ctx.Set<DecisaoDoGateEntity>().SingleAsync(d => d.FluxoId == c.Fluxo.Id, Ct);
            decisao.Decisao.Should().Be(DecisaoGate.Bloqueado);
            decisao.Motivo.Should().Be("gate.bloqueado");
            (await EstadoDaEtapaAsync(c, EtapaDoFluxo.Qa)).Should().Be(EstadoEtapa.Bloqueada);
        }

        [Fact, DisplayName("6: o reenvio do mesmo job substitui a Analise, os Achados e a decisao: uma Analise por chave")]
        public async Task Reenvio_Substitui()
        {
            var c = await CenarioAbertoAsync();
            (await PostAsync(c, "gate", Corpo(c, "701", Analise("opengrep", "reprovado", "ferramenta", Achado("medium"), Achado("low"))))).Status.Should().Be(HttpStatusCode.OK);

            var (status, _) = await PostAsync(c, "gate", Corpo(c, "701", Analise("opengrep", "reprovado", "ferramenta", Achado("low"))));

            status.Should().Be(HttpStatusCode.OK);
            (await AnalisesAsync(c)).Should().ContainSingle().Which.Achados.Should().ContainSingle();
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var decisao = await ctx.Set<DecisaoDoGateEntity>().SingleAsync(d => d.FluxoId == c.Fluxo.Id, Ct);
            (decisao.Media, decisao.Baixa).Should().Be((0, 1));
        }

        [Fact, DisplayName("6: dois gates identicos simultaneos terminam sem 500 e com uma Analise so")]
        public async Task Simultaneos_SemErro500()
        {
            var c = await CenarioAbertoAsync();
            var corpo = Corpo(c, "701", Analise("opengrep", "reprovado", "ferramenta", Achado("medium")));

            var respostas = await Task.WhenAll(PostAsync(c, "gate", corpo), PostAsync(c, "gate", corpo));

            respostas.Should().OnlyContain(r => (int)r.Status < 500);
            respostas.Should().Contain(r => r.Status == HttpStatusCode.OK);
            (await AnalisesAsync(c)).Should().ContainSingle();
        }

        [Fact, DisplayName("5: sem id_token responde 401 pipeline.token-invalido")]
        public async Task SemToken_401()
        {
            var c = await CenarioAbertoAsync();

            var (status, texto) = await PostAsync(c, "gate", Corpo(c, "701", Analise("opengrep")), comToken: false);

            status.Should().Be(HttpStatusCode.Unauthorized);
            JsonNode.Parse(texto)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.token-invalido");
            (await AnalisesAsync(c)).Should().BeEmpty();
        }

        [Fact, DisplayName("4: corpo acima de 2 MB responde 422 pipeline.corpo-invalido sem gravar")]
        public async Task CorpoGrande_422()
        {
            var c = await CenarioAbertoAsync();
            var corpo = Corpo(c, "701", Analise("opengrep"));
            corpo["lixo"] = new string('x', 2 * 1024 * 1024 + 10);

            var (status, texto) = await PostAsync(c, "gate", corpo);

            status.Should().Be((HttpStatusCode)422);
            JsonNode.Parse(texto)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("pipeline.corpo-invalido");
            (await AnalisesAsync(c)).Should().BeEmpty();
        }

        [Fact, DisplayName("4: sem Analises responde 422 gate.sem-analises sem gravar")]
        public async Task SemAnalises_422()
        {
            var c = await CenarioAbertoAsync();

            var (status, texto) = await PostAsync(c, "gate", Corpo(c, "701"));

            status.Should().Be((HttpStatusCode)422);
            JsonNode.Parse(texto)!["errors"]![0]!["code"]!.GetValue<string>().Should().Be("gate.sem-analises");
            (await AnalisesAsync(c)).Should().BeEmpty();
        }
    }
}
