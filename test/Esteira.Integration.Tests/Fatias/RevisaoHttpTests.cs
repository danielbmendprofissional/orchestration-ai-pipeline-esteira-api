using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Fatias
{
    // P2.5 na borda HTTP: as três rotas de decisão, os campos novos do Painel e da gaveta e a flag Features:P2, com sessão
    // simulada, banco real e IPainelDeFatias falso (o GitLab nunca é chamado).
    [Trait("Fatia", "P2")]
    public sealed class RevisaoHttpTests : IClassFixture<ApiDeTeste>
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

        private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        private readonly InfraestruturaFixture _infra;
        private readonly PainelFalso _painel = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public RevisaoHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string? p2) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:P1", "true");
            b.UseSetting("Features:P2", p2 ?? string.Empty);
            b.UseSetting("Review:UrlModelo", "https://{fatia}.preview.exemplo.com");
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

        private static Task<HttpResponseMessage> EnviarAsync(WebApplicationFactory<Program> api, HttpMethod metodo, string url, string sessao, object? corpo = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            if (corpo is not null)
                pedido.Content = JsonContent.Create(corpo);
            return cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        // Aplicação com workspace, Projetos Api e Frontend, a Pessoa e os papéis. Devolve o PublicId.
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
        private void Programar(Guid app, string ci = "success")
        {
            var mrs = new[]
            {
                new MrDaGaveta(RepositorioFatia.Api, 3, "P2 — T", "opened", "feature/p2-t", ci),
                new MrDaGaveta(RepositorioFatia.Frontend, 8, "P2 — T", "opened", "feature/p2-t", ci),
            };
            var fatia = new FatiaLida("P2", "Título", "—", true, [RepositorioFatia.Api, RepositorioFatia.Frontend], "alto", true, "specs/x/p2.md", 7,
                new ResultadoDeEstado(EstadoDaFatia.EmRevisao, null, false, false), false, new DetalheDaFatia(null, null, [], [], mrs));
            _painel.Resultados[app.ToString()] = new ResultadoDoPainel(app.ToString(), SituacaoDoPainel.Ok, [fatia], Agora, false, null);
        }

        private static string Rota(Guid app, string acao) => $"/api/v1/aplicacoes/{app}/fatias/P2/{acao}";

        [Fact, DisplayName("Critério 16: Features:P2 ausente responde 404 nas três rotas, mesmo para MOR")]
        public async Task Flag_desligada_404_nas_tres_rotas()
        {
            var app = await AplicacaoAsync("flag-off");
            Programar(app);
            var sessao = await SessaoAsync(_desligada, "sub-mor-r1", "mor");

            (await EnviarAsync(_desligada, HttpMethod.Post, Rota(app, "review"), sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await EnviarAsync(_desligada, HttpMethod.Post, Rota(app, "aprovar"), sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await EnviarAsync(_desligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Sem papel na Aplicação é 404; com papel mas sem a operação é 403 acesso.papel-insuficiente")]
        public async Task Sem_papel_404_e_sem_operacao_403()
        {
            var membro = $"sub-r-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("papeis", (membro, PapelAplicacao.Membro));
            Programar(app);

            var estranho = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "aprovar"), await SessaoAsync(_ligada, "sub-estranho-r"));
            var semOperacao = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "aprovar"), await SessaoAsync(_ligada, membro));
            var reprovar = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), await SessaoAsync(_ligada, membro), new { comentario = "x" });

            estranho.StatusCode.Should().Be(HttpStatusCode.NotFound);
            semOperacao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CorpoAsync(semOperacao)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("acesso.papel-insuficiente");
            reprovar.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact, DisplayName("Abrir review: Membro recebe 202 com uma Ação por repositório, API primeiro; repetir dá 409 fatia.review-ja-pedido")]
        public async Task Abrir_review_202()
        {
            var membro = $"sub-r-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("abrir", (membro, PapelAplicacao.Membro));
            Programar(app);
            var sessao = await SessaoAsync(_ligada, membro);

            var resposta = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "review"), sessao);
            var repetida = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "review"), sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var acoes = (await CorpoAsync(resposta)).GetProperty("acoes").EnumerateArray().ToList();
            acoes.Select(a => a.GetProperty("repositorio").GetString()).Should().Equal("api", "frontend");
            acoes.Should().OnlyContain(a => a.GetProperty("tipo").GetString() == "abrir-review" && a.GetProperty("estado").GetString() == "pedida");
            acoes.Select(a => a.GetProperty("mrIid").GetInt64()).Should().Equal(3, 8);
            repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CorpoAsync(repetida)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("fatia.review-ja-pedido");
        }

        [Fact, DisplayName("Aprovar: Aprovador técnico recebe 202 com uma Ação por MR; CI vermelho é 409 fatia.ci-nao-verde")]
        public async Task Aprovar_202_e_ci_vermelho_409()
        {
            var aprovador = $"sub-r-{Guid.NewGuid():N}";
            var verde = await AplicacaoAsync("aprova-verde", (aprovador, PapelAplicacao.AprovadorTecnico));
            var vermelha = await AplicacaoAsync("aprova-vermelha", (aprovador, PapelAplicacao.AprovadorTecnico));
            Programar(verde);
            Programar(vermelha, "failed");
            var sessao = await SessaoAsync(_ligada, aprovador);

            var ok = await EnviarAsync(_ligada, HttpMethod.Post, Rota(verde, "aprovar"), sessao);
            var ci = await EnviarAsync(_ligada, HttpMethod.Post, Rota(vermelha, "aprovar"), sessao);

            ok.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await CorpoAsync(ok)).GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("tipo").GetString()).Should().Equal("aprovar", "aprovar");
            ci.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CorpoAsync(ci)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("fatia.ci-nao-verde");
        }

        [Fact, DisplayName("Reprovar: 202 com comentário; sem comentário, vazio ou sem corpo é 400; duplo clique devolve as mesmas Ações; Aprovar depois é 409 em andamento")]
        public async Task Reprovar_202_e_comentario_invalido_400()
        {
            var aprovador = $"sub-r-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("reprova", (aprovador, PapelAplicacao.AprovadorTecnico));
            Programar(app);
            var sessao = await SessaoAsync(_ligada, aprovador);

            var vazio = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = "   " });
            var semCorpo = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao);
            var grande = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = new string('x', 2001) });
            var ok = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = "<b>refaça</b> o teste" });
            var duploClique = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = "<b>refaça</b> o teste" });
            var andamento = await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "aprovar"), sessao);

            foreach (var r in new[] { vazio, semCorpo, grande })
            {
                r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                (await CorpoAsync(r)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("fatia.comentario-invalido");
            }
            ok.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await CorpoAsync(ok)).GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("tipo").GetString()).Should().Equal("reprovar", "reprovar");
            duploClique.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await CorpoAsync(duploClique)).GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("id").GetGuid())
                .Should().Equal((await CorpoAsync(ok)).GetProperty("acoes").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
            andamento.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CorpoAsync(andamento)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("fatia.decisao-em-andamento");
        }

        [Fact, DisplayName("Critérios 12 e 14: Painel e gaveta trazem acoes, review, decisao e a lista de Ações (o comentário só na Ação de Reprovar)")]
        public async Task Painel_e_gaveta_com_flag_ligada()
        {
            var aprovador = $"sub-r-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("campos", (aprovador, PapelAplicacao.AprovadorTecnico));
            Programar(app);
            var sessao = await SessaoAsync(_ligada, aprovador);
            await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "review"), sessao);
            await EnviarAsync(_ligada, HttpMethod.Post, Rota(app, "reprovar"), sessao, new { comentario = "refaça" });

            var painel = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/fatias?aplicacoes={app}&aba=todas", sessao));
            var gaveta = await CorpoAsync(await EnviarAsync(_ligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/P2", sessao));

            var linha = painel.GetProperty("itens")[0];
            linha.GetProperty("decisao").GetString().Should().Be("reprovando");
            linha.GetProperty("review").GetProperty("estado").GetString().Should().Be("pedido");
            linha.GetProperty("acoes").GetProperty("podeAprovar").GetProperty("motivo").GetString().Should().Be("decisao-em-andamento");
            linha.GetProperty("acoes").GetProperty("podeAbrirReview").GetProperty("pode").GetBoolean().Should().BeFalse();
            gaveta.GetProperty("decisao").GetString().Should().Be("reprovando");
            var lista = gaveta.GetProperty("acoesPedidas").EnumerateArray().ToList();
            lista.Should().HaveCount(4);
            lista.Where(a => a.GetProperty("tipo").GetString() == "abrir-review")
                .Select(a => (Sem: !a.TryGetProperty("comentario", out _), Quem: a.GetProperty("pedidaPor").GetString()!)).Should().OnlyContain(a => a.Sem && a.Quem.StartsWith("Pessoa"));
            lista.Where(a => a.GetProperty("tipo").GetString() == "reprovar")
                .Should().OnlyContain(a => a.GetProperty("comentario").GetString() == "refaça");
        }

        [Fact, DisplayName("Critério 16: com Features:P2 desligada os campos novos vêm vazios no Painel e na gaveta")]
        public async Task Painel_e_gaveta_com_flag_desligada()
        {
            var membro = $"sub-r-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync("campos-off", (membro, PapelAplicacao.Membro));
            Programar(app);
            var sessao = await SessaoAsync(_desligada, membro);

            var painel = await CorpoAsync(await EnviarAsync(_desligada, HttpMethod.Get, $"/api/v1/fatias?aplicacoes={app}&aba=todas", sessao));
            var gaveta = await CorpoAsync(await EnviarAsync(_desligada, HttpMethod.Get, $"/api/v1/aplicacoes/{app}/fatias/P2", sessao));

            var linha = painel.GetProperty("itens")[0];
            foreach (var campo in new[] { "acoes", "review", "decisao" })
            {
                linha.TryGetProperty(campo, out _).Should().BeFalse();
                gaveta.TryGetProperty(campo, out _).Should().BeFalse();
            }
            gaveta.GetProperty("acoesPedidas").EnumerateArray().Should().BeEmpty();
        }
    }
}
