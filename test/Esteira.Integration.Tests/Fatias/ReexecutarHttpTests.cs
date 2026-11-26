using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Esteira.Application.Fatias;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Fatias;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Fatias
{
    // P2b.5 na borda HTTP: a rota Reexecutar (flag, 404, 403, 202, duplo clique), `podeReexecutar` na gaveta e o contrato OpenAPI.
    // Sessão simulada, banco real e IPainelDeFatias falso (o GitLab nunca é chamado).
    [Trait("Fatia", "P2b")]
    public sealed class ReexecutarHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed class PainelFalso : IPainelDeFatias
        {
            public Dictionary<string, ResultadoDoPainel> Resultados { get; } = [];

            public Task<ResultadoDoPainel> ObterAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
                => Task.FromResult(Resultados[aplicacao.Chave]);

            public Task<IReadOnlyList<ResultadoDoPainel>> ObterAsync(IReadOnlyList<AplicacaoDoPainel> aplicacoes, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<ResultadoDoPainel>>(aplicacoes.Select(a => Resultados[a.Chave]).ToList());

            public Task<ResultadoDoPainel> ObterFrescoAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
                => Task.FromResult(Resultados[aplicacao.Chave]);

            public Task<Result> AtualizarAsync(AplicacaoDoPainel aplicacao, CancellationToken cancellationToken = default)
                => Task.FromResult(Result.Sucesso());
        }

        private static readonly DateTimeOffset Agora = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        private readonly InfraestruturaFixture _infra;
        private readonly ApiDeTeste _api;
        private readonly PainelFalso _painel = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public ReexecutarHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string? p2) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:P1", "true");
            b.UseSetting("Features:P2", p2 ?? string.Empty);
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

        private static Task<HttpResponseMessage> EnviarAsync(WebApplicationFactory<Program> api, HttpMethod metodo, string url, string sessao)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            return cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private async Task<Guid> AplicacaoAsync(string nome, params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupo = $"g{Guid.NewGuid():N}"[..10];
            var app = AplicacaoEntity.Criar(nome, ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), grupo).Value;
            app.DefinirWorkspace(ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-workspace");
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-api", $"{nome}-api", Stack.Dotnet));
            ctx.Add(ProjetoEntity.Criar(app.Id, ContextoDoCadastro.IdUnico(), $"{grupo}/{nome}-frontend", $"{nome}-frontend", Stack.Angular));
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                ctx.Add(PoliticaSeveridadeEntity.Padrao(app.Id, amb));
            foreach (var (sub, papel) in papeis)
            {
                if (!await ctx.Set<PessoaEntity>().AnyAsync(p => p.Sub == sub, Ct))
                    ctx.Add(PessoaEntity.Criar(sub, $"Pessoa {sub[^4..]}", $"{sub}@x.com", DateTime.UtcNow));
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            }
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        // Fatia `P2` Em revisão com uma MR aberta em cada repositório, CI verde.
        private void Programar(Guid app)
        {
            var mrs = new[]
            {
                new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — T", "opened", "feature/p2-t", "success"),
                new MrDaGaveta(RepositorioFatia.Frontend, 8, "P2 — T", "opened", "feature/p2-t", "success"),
            };
            var fatia = new FatiaLida("P2", "Título", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x/p2.md", 7,
                new ResultadoDeEstado(EstadoDaFatia.EmRevisao, null, false, false), false, new DetalheDaFatia(null, null, [], [], mrs));
            _painel.Resultados[app.ToString()] = new ResultadoDoPainel(app.ToString(), SituacaoDoPainel.Ok, [fatia], Agora, false, null);
        }

        // Grava um Aprovar que falhou na API e o do front cancelado (como a P2b deixa). Devolve o PublicId da Ação da API.
        private async Task<Guid> AprovacaoFalhadaAsync(Guid appPublicId, string sub)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = await ctx.Set<AplicacaoEntity>().SingleAsync(a => a.PublicId == appPublicId, Ct);
            var projetos = await ctx.Set<ProjetoEntity>().Where(p => p.AplicacaoId == app.Id).ToListAsync(Ct);
            var pessoa = await ctx.Set<PessoaEntity>().SingleAsync(p => p.Sub == sub, Ct);
            var agora = DateTime.UtcNow;

            var api = AcaoBotEntity.Criar(app.Id, "P2", projetos.Single(p => p.Stack == Stack.Dotnet).Id, 3, TipoAcaoBot.Aprovar, 1, null, pessoa.Id, null, agora).Value;
            ctx.Add(api);
            await ctx.SaveChangesAsync(Ct);
            api.Iniciar(agora);
            api.Falhar("gitlab.indisponivel", agora);
            var front = AcaoBotEntity.Criar(app.Id, "P2", projetos.Single(p => p.Stack == Stack.Angular).Id, 8, TipoAcaoBot.Aprovar, 1, api.Id, pessoa.Id, null, agora).Value;
            front.Cancelar(agora);
            ctx.Add(front);
            await ctx.SaveChangesAsync(Ct);
            return api.PublicId;
        }

        private static string Rota(Guid app, Guid acao) => $"/api/v1/aplicacoes/{app}/fatias/P2/acoes/{acao}/reexecutar";

        [Fact, DisplayName("16: Features:P2 ausente responde 404 no Reexecutar, mesmo para MOR")]
        public async Task Flag_desligada_404()
        {
            var app = await AplicacaoAsync("rex-flag-off");
            Programar(app);

            var r = await EnviarAsync(_desligada, HttpMethod.Post, Rota(app, Guid.NewGuid()), await SessaoAsync(_desligada, "sub-mor-rex", "mor"));

            r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("14: sem papel é 404; Membro sem a operação do Aprovar é 403 acesso.papel-insuficiente; Ação desconhecida é 404 acao.nao-encontrada")]
        public async Task Sem_papel_sem_operacao_e_desconhecida()
        {
            var membro = $"sub-x-{Guid.NewGuid():N}";
            var aprovador = $"sub-x-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("rex-papeis", (membro, PapelAplicacao.Membro), (aprovador, PapelAplicacao.AprovadorTecnico));
            Programar(app);
            var falhada = await AprovacaoFalhadaAsync(app, aprovador);

            var estranho = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, falhada), await SessaoAsync(_ligada, "sub-estranho-rex"));
            var semOperacao = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, falhada), await SessaoAsync(_ligada, membro));
            var desconhecida = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, Guid.NewGuid()), await SessaoAsync(_ligada, aprovador));

            estranho.StatusCode.Should().Be(HttpStatusCode.NotFound);
            semOperacao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CorpoAsync(semOperacao)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("acesso.papel-insuficiente");
            desconhecida.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CorpoAsync(desconhecida)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("acao.nao-encontrada");
        }

        [Fact, DisplayName("13 e 15: 202 recria a Ação da API e a do front (tentativa 2); repetir devolve as mesmas Ações; a Ação nova não é reexecutável (409)")]
        public async Task Reexecutar_202_e_duplo_clique()
        {
            var aprovador = $"sub-x-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("rex-ok", (aprovador, PapelAplicacao.AprovadorTecnico));
            Programar(app);
            var falhada = await AprovacaoFalhadaAsync(app, aprovador);
            var sessao = await SessaoAsync(_ligada, aprovador);

            var primeira = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, falhada), sessao);
            var repetida = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, falhada), sessao);

            primeira.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var acoes = (await CorpoAsync(primeira)).GetProperty("acoes").EnumerateArray().ToList();
            acoes.Select(a => a.GetProperty("repositorio").GetString()).Should().Equal("api", "frontend");
            acoes.Should().OnlyContain(a => a.GetProperty("tipo").GetString() == "aprovar" && a.GetProperty("estado").GetString() == "pedida");
            repetida.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await CorpoAsync(repetida)).GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("id").GetGuid())
                .Should().Equal(acoes.Select(a => a.GetProperty("id").GetGuid()));

            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var gravadas = await ctx.Set<AcaoBotEntity>().Where(a => a.PublicId == acoes[0].GetProperty("id").GetGuid() || a.PublicId == acoes[1].GetProperty("id").GetGuid())
                .OrderBy(a => a.Id).ToListAsync(Ct);
            gravadas.Select(a => a.ChaveIdempotencia.Split(':')[^1]).Should().Equal("2", "2");
            gravadas.Select(a => a.ReexecucaoDeAcaoId).Should().AllSatisfy(r => r.Should().NotBeNull());
            gravadas[1].DependeDeAcaoId.Should().Be(gravadas[0].Id);
        }

        [Fact, DisplayName("18: a gaveta traz podeReexecutar por Ação (pode na falhada; motivo quando não pode)")]
        public async Task Gaveta_com_pode_reexecutar()
        {
            var aprovador = $"sub-x-{Guid.NewGuid():N}";
            var membro = $"sub-x-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("rex-gaveta", (aprovador, PapelAplicacao.AprovadorTecnico), (membro, PapelAplicacao.Membro));
            Programar(app);
            var falhada = await AprovacaoFalhadaAsync(app, aprovador);

            var doAprovador = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/P2", await SessaoAsync(_ligada, aprovador)));
            var doMembro = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/P2", await SessaoAsync(_ligada, membro)));

            var aFalhada = doAprovador.GetProperty("acoesPedidas").EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == falhada);
            aFalhada.GetProperty("podeReexecutar").GetProperty("pode").GetBoolean().Should().BeTrue();
            var cancelada = doAprovador.GetProperty("acoesPedidas").EnumerateArray().Single(a => a.GetProperty("estado").GetString() == "cancelada");
            cancelada.GetProperty("podeReexecutar").GetProperty("motivo").GetString().Should().Be("nao-falhou");
            doMembro.GetProperty("acoesPedidas").EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == falhada)
                .GetProperty("podeReexecutar").GetProperty("motivo").GetString().Should().Be("papel");
        }

        [Fact, DisplayName("Contrato: o OpenAPI descreve a rota Reexecutar (202 AcoesPedidasDto, erros RespostaDeErro) e podeReexecutar na Ação da gaveta")]
        public async Task Contrato_openapi()
        {
            var doc = JsonNode.Parse(await _api.CreateClient().GetStringAsync("/openapi/v1.json", Ct))!;

            var respostas = doc["paths"]!["/api/v1/aplicacoes/{id}/fatias/{codigo}/acoes/{acaoId}/reexecutar"]!["post"]!["responses"]!.AsObject();

            respostas["202"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/AcoesPedidasDto");
            foreach (var erro in new[] { "401", "403", "404", "409" })
                respostas[erro]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/RespostaDeErro");
            doc["components"]!["schemas"]!["AcaoDaFatiaDto"]!["properties"]!.AsObject().ContainsKey("podeReexecutar").Should().BeTrue();
        }
    }
}
