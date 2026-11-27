using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Pipeline
{
    // E1.3, critérios 5, 6, 16 e 17: passos 1 (id_token + Projeto) e 2 (corpo) do POST /api/pipeline/v2/abrir.
    [Trait("Fatia", "E1")]
    public sealed class AbrirPipelineHttpTests : IClassFixture<PipelineApiDeTeste>
    {
        private const string Rota = "/api/pipeline/v2/abrir";
        private const string Sha = "0123456789abcdef0123456789abcdef01234567";

        private readonly PipelineApiDeTeste _api;
        private readonly InfraestruturaFixture _infra;
        private readonly HttpClient _cliente;

        public AbrirPipelineHttpTests(PipelineApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
            _cliente = api.CreateClient();
            _api.Consultas.Zerar();
            lock (_api.Execucoes.Recebidas)
                _api.Execucoes.Recebidas.Clear();
        }

        private static string NovoCaminho() => $"grupo/proj-{Guid.NewGuid():N}";

        private async Task CadastrarProjetoAsync(string caminho, bool ativo = true)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            var projeto = ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), caminho, "proj", Stack.Dotnet);
            if (!ativo)
                projeto.Alterar(Stack.Dotnet, false);
            ctx.Add(projeto);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        private static JsonObject CorpoValido(string projeto) => new()
        {
            ["process"] = "fluxo",
            ["env"] = "qa",
            ["entrega_id"] = Guid.NewGuid().ToString(),
            ["fluxo_id"] = Guid.NewGuid().ToString(),
            ["sha"] = Sha,
            ["projeto"] = projeto,
            ["pipeline_id"] = "4711",
            ["versao"] = "",
            ["atalho_por"] = "",
            ["atalho_motivo"] = "",
            ["flag_somente_leitura"] = false
        };

        private Task<HttpResponseMessage> PostAsync(string? authorization, string corpo)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, Rota)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            };
            if (authorization is not null)
                pedido.Headers.TryAddWithoutValidation("Authorization", authorization);
            return _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        }

        private Task<HttpResponseMessage> PostAsync(string idToken, JsonObject corpo)
            => PostAsync("Bearer " + idToken, corpo.ToJsonString());

        private static async Task<(HttpStatusCode Status, string Texto)> LerAsync(HttpResponseMessage resposta)
            => (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        private static string CodigoDoErro(string texto) => JsonNode.Parse(texto)!["errors"]![0]!["code"]!.GetValue<string>();

        public static IEnumerable<TheoryDataRow<string>> DefeitosDoToken() =>
        [
            new("sem-cabecalho"), new("esquema-errado"), new("bearer-vazio"), new("lixo"), new("assinatura"),
            new("expirado"), new("audiencia"), new("emissor")
        ];

        private string? AuthorizationDoDefeito(string defeito, string caminho)
        {
            var e = _api.Emissor;
            return defeito switch
            {
                "sem-cabecalho" => null,
                "esquema-errado" => "Basic dXNlcjpzZW5oYQ==",
                "bearer-vazio" => "Bearer ",
                "lixo" => "Bearer isto-nao-e-um-jwt",
                "assinatura" => "Bearer " + e.IdToken(caminho, chave: EmissorJwksFalso.ChaveEstranha()),
                "expirado" => "Bearer " + e.IdToken(caminho, expiraEm: TimeSpan.FromHours(-1)),
                "audiencia" => "Bearer " + e.IdToken(caminho, audiencia: "outra-audiencia"),
                "emissor" => "Bearer " + e.IdToken(caminho, emissor: "https://gitlab.outro"),
                _ => throw new ArgumentOutOfRangeException(nameof(defeito))
            };
        }

        [Theory, DisplayName("Deve responder 401 pipeline.token-invalido para cada defeito do id_token, sem consultar o banco")]
        [MemberData(nameof(DefeitosDoToken))]
        public async Task TokenInvalido_Deve401SemConsultarOBanco(string defeito)
        {
            var caminho = NovoCaminho();
            await CadastrarProjetoAsync(caminho);

            var resposta = await PostAsync(AuthorizationDoDefeito(defeito, caminho), CorpoValido(caminho).ToJsonString());
            var (status, texto) = await LerAsync(resposta);

            status.Should().Be(HttpStatusCode.Unauthorized);
            CodigoDoErro(texto).Should().Be("pipeline.token-invalido");
            _api.Consultas.Chamadas.Should().Be(0);
            _api.Execucoes.Recebidas.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve dar a mesma resposta 403 pipeline.projeto-nao-reconhecido para caminho diferente do token, Projeto inexistente e Projeto inativo")]
        public async Task ProjetoNaoReconhecido_Deve403Uniforme()
        {
            var ativo = NovoCaminho();
            var inativo = NovoCaminho();
            var inexistente = NovoCaminho();
            await CadastrarProjetoAsync(ativo);
            await CadastrarProjetoAsync(inativo, ativo: false);

            var diferente = await LerAsync(await PostAsync(_api.Emissor.IdToken(inexistente), CorpoValido(ativo)));
            var naoCadastrado = await LerAsync(await PostAsync(_api.Emissor.IdToken(inexistente), CorpoValido(inexistente)));
            var desativado = await LerAsync(await PostAsync(_api.Emissor.IdToken(inativo), CorpoValido(inativo)));

            foreach (var (status, texto) in new[] { diferente, naoCadastrado, desativado })
            {
                status.Should().Be(HttpStatusCode.Forbidden);
                CodigoDoErro(texto).Should().Be("pipeline.projeto-nao-reconhecido");
            }

            naoCadastrado.Texto.Should().Be(diferente.Texto);
            desativado.Texto.Should().Be(diferente.Texto);
            _api.Execucoes.Recebidas.Should().BeEmpty();
        }

        public static IEnumerable<TheoryDataRow<string, string?>> CorposInvalidos() =>
        [
            new("sha", "ABCDEF0123456789abcdef0123456789abcdef01"),
            new("sha", "0123456789abcdef0123456789abcdef0123456"),
            new("sha", "g123456789abcdef0123456789abcdef01234567"),
            new("sha", null),
            new("env", "dev"),
            new("env", ""),
            new("process", "deploy"),
            new("process", null),
            new("entrega_id", "nao-e-guid"),
            new("fluxo_id", "1234"),
            new("fluxo_id", null)
        ];

        [Theory, DisplayName("Deve responder 422 pipeline.corpo-invalido para corpo inválido, sem consultar o banco")]
        [MemberData(nameof(CorposInvalidos))]
        public async Task CorpoInvalido_Deve422SemConsultarOBanco(string campo, string? valor)
        {
            var caminho = NovoCaminho();
            await CadastrarProjetoAsync(caminho);
            var corpo = CorpoValido(caminho);
            corpo[campo] = valor;

            var (status, texto) = await LerAsync(await PostAsync(_api.Emissor.IdToken(caminho), corpo));

            status.Should().Be(HttpStatusCode.UnprocessableEntity);
            CodigoDoErro(texto).Should().Be("pipeline.corpo-invalido");
            _api.Consultas.Chamadas.Should().Be(0);
            _api.Execucoes.Recebidas.Should().BeEmpty();
        }

        [Theory, DisplayName("Deve responder 422 pipeline.corpo-invalido para corpo ilegível ou vazio")]
        [InlineData("")]
        [InlineData("{ isto nao e json")]
        [InlineData("null")]
        [InlineData("[]")]
        public async Task CorpoIlegivel_Deve422(string corpo)
        {
            var caminho = NovoCaminho();
            await CadastrarProjetoAsync(caminho);

            var (status, texto) = await LerAsync(await PostAsync("Bearer " + _api.Emissor.IdToken(caminho), corpo));

            status.Should().Be(HttpStatusCode.UnprocessableEntity);
            CodigoDoErro(texto).Should().Be("pipeline.corpo-invalido");
            _api.Consultas.Chamadas.Should().Be(0);
        }

        [Fact, DisplayName("Deve repassar a entrada validada ao ponto de extensão e devolver a resposta dele quando token, Projeto e corpo são válidos")]
        public async Task Valido_DeveRepassarAoPontoDeExtensao()
        {
            var caminho = NovoCaminho();
            await CadastrarProjetoAsync(caminho);
            var corpo = CorpoValido(caminho);

            var (status, texto) = await LerAsync(await PostAsync(_api.Emissor.IdToken(caminho), corpo));

            status.Should().Be(HttpStatusCode.OK);
            var json = JsonNode.Parse(texto)!;
            json["feature_flags"]!.GetValue<string>().Should().Be("B2=true");
            json["destinos"]![0]!["nome"]!.GetValue<string>().Should().Be("local");
            json["destinos"]![0]!["configuracao"]!["K8S_NAMESPACE"]!.GetValue<string>().Should().Be("qa");
            var entrada = _api.Execucoes.Recebidas.Should().ContainSingle().Subject;
            entrada.Process.Should().Be("fluxo");
            entrada.Env.Should().Be("qa");
            entrada.Sha.Should().Be(Sha);
            entrada.Projeto.Should().Be(caminho);
            entrada.FluxoId.Should().Be(Guid.Parse(corpo["fluxo_id"]!.GetValue<string>()));
        }

        [Fact, DisplayName("Não deve ter o id_token na resposta nem no log, em sucesso e em cada recusa")]
        public async Task IdToken_NaoVazaNaRespostaNemNoLog()
        {
            var caminho = NovoCaminho();
            await CadastrarProjetoAsync(caminho);
            var marcador = Guid.NewGuid().ToString("N");
            var extras = new Dictionary<string, object> { ["marcador"] = marcador };
            var valido = _api.Emissor.IdToken(caminho, extras: extras);
            var assinaturaRuim = _api.Emissor.IdToken(caminho, chave: EmissorJwksFalso.ChaveEstranha(), extras: extras);
            var expirado = _api.Emissor.IdToken(caminho, expiraEm: TimeSpan.FromHours(-1), extras: extras);
            var outroProjeto = _api.Emissor.IdToken(NovoCaminho(), extras: extras);
            var corpoRuim = CorpoValido(caminho);
            corpoRuim["sha"] = "x";

            var respostas = new List<string>();
            foreach (var (token, corpo) in new[]
                     {
                         (valido, CorpoValido(caminho)), (assinaturaRuim, CorpoValido(caminho)), (expirado, CorpoValido(caminho)),
                         (outroProjeto, CorpoValido(caminho)), (valido, corpoRuim)
                     })
            {
                var resposta = await PostAsync(token, corpo);
                respostas.Add(resposta + await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }

            var log = _api.Services.GetRequiredService<LogDeTeste>().Eventos
                .Select(e => e.RenderMessage() + string.Join(" ", e.Properties.Select(p => p.Value.ToString())) + e.Exception)
                .ToList();
            log.Should().NotBeEmpty();
            var partesSensiveis = new[] { valido, assinaturaRuim, expirado, outroProjeto }
                .SelectMany(t => new[] { t, t.Split('.')[1], t.Split('.')[2] })
                .Append(marcador);

            foreach (var parte in partesSensiveis)
            {
                respostas.Should().NotContain(r => r.Contains(parte));
                log.Should().NotContain(l => l.Contains(parte));
            }
        }

        private async Task<JsonNode> LerOpenApiAsync(string documento)
        {
            var json = await _cliente.GetStringAsync($"/openapi/{documento}.json", TestContext.Current.CancellationToken);
            return JsonNode.Parse(json)!;
        }

        [Fact, DisplayName("Deve publicar o documento OpenAPI pipeline-v2 só com as rotas /api/pipeline/v2, separado do v1")]
        public async Task OpenApi_PipelineV2SeparadoDoV1()
        {
            var v2 = await LerOpenApiAsync("pipeline-v2");
            var v1 = await LerOpenApiAsync("v1");

            var rotasV2 = v2["paths"]!.AsObject().Select(p => p.Key).ToArray();
            rotasV2.Should().Contain("/api/pipeline/v2/abrir");
            rotasV2.Should().OnlyContain(r => r.StartsWith("/api/pipeline/v2/"));
            v2["paths"]!["/api/pipeline/v2/abrir"]!["post"].Should().NotBeNull();

            v1["paths"]!.AsObject().Select(p => p.Key).Should().NotContain(r => r.StartsWith("/api/pipeline/"));
        }
    }
}
