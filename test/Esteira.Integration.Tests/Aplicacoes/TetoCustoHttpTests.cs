using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // X3.2 na borda HTTP: PUT /aplicacoes/{id}/teto-custo, sob Features:X3, com sessão simulada. Precisa do Docker (Testcontainers).
    [Trait("Fatia", "X3")]
    public sealed class TetoCustoHttpTests : IClassFixture<ApiDeTeste>
    {
        // Decorador do repositório que segura as duas requisições depois de lerem a Aplicação, antes de qualquer gravação.
        private sealed class RepositorioComBarreira(AplicacaoRepositorio interno, BarreiraDeLeitura barreira) : IAplicacaoRepositorio
        {
            public async Task<AplicacaoEntity?> ObterAsync(long id, CancellationToken cancellationToken = default)
            {
                var app = await interno.ObterAsync(id, cancellationToken);
                barreira.Aguardar(cancellationToken);
                return app;
            }

            public Task<bool> SiglaExisteAsync(string sigla, long? exceto, CancellationToken cancellationToken = default) => interno.SiglaExisteAsync(sigla, exceto, cancellationToken);
            public Task<bool> GrupoVinculadoAsync(long grupoGitLabId, CancellationToken cancellationToken = default) => interno.GrupoVinculadoAsync(grupoGitLabId, cancellationToken);
            public Task<decimal?> ObterTetoCustoGravadoAsync(long id, CancellationToken cancellationToken = default) => interno.ObterTetoCustoGravadoAsync(id, cancellationToken);
            public Task AdicionarAsync(AplicacaoEntity aplicacao, CancellationToken cancellationToken = default) => interno.AdicionarAsync(aplicacao, cancellationToken);
            public Task AdicionarPoliticaAsync(PoliticaSeveridadeEntity politica, CancellationToken cancellationToken = default) => interno.AdicionarPoliticaAsync(politica, cancellationToken);
            public Task<PoliticaSeveridadeEntity?> ObterPoliticaAsync(long aplicacaoId, AmbientePolitica ambiente, CancellationToken cancellationToken = default) => interno.ObterPoliticaAsync(aplicacaoId, ambiente, cancellationToken);
        }

        private sealed class BarreiraDeLeitura
        {
            private volatile Barrier? _barreira;

            public void Armar(int participantes) => _barreira = new Barrier(participantes);

            public void Aguardar(CancellationToken ct)
            {
                var b = _barreira;
                if (b is not null && !b.SignalAndWait(TimeSpan.FromSeconds(30), ct))
                    throw new TimeoutException("A outra requisição não chegou à barreira.");
            }
        }

        private readonly InfraestruturaFixture _infra;
        private readonly BarreiraDeLeitura _barreira = new();
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public TetoCustoHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = Com(api, "true");
            _desligada = Com(api, null);
        }

        private WebApplicationFactory<Program> Com(ApiDeTeste api, string? x3) => api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Features:A1", "true");
            b.UseSetting("Features:X3", x3 ?? string.Empty);
            b.UseSetting("Pipeline:TetoCustoPadraoUsd", "2.00");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAplicacaoRepositorio>();
                s.AddScoped<IAplicacaoRepositorio>(sp => new RepositorioComBarreira(
                    new AplicacaoRepositorio(sp.GetRequiredService<EsteiraContext>()), _barreira));
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

        private static Task<HttpResponseMessage> PutAsync(WebApplicationFactory<Program> api, Guid id, string sessao, string corpoJson)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/aplicacoes/{id}/teto-custo");
            pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            pedido.Content = new StringContent(corpoJson, Encoding.UTF8, "application/json");
            return cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private static async Task<string> CodigoDoErroAsync(HttpResponseMessage resposta)
            => (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString()!;

        private async Task<Guid> AplicacaoAsync(params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "g").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        [Fact, DisplayName("Critério 1 e 6: MOR define o teto (200 {tetoCustoUsd, padraoUsd, efetivoUsd}) e a auditoria guarda antes e depois")]
        public async Task Mor_Define()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, $"sub-mor-teto-{Guid.NewGuid():N}", "mor");

            var resposta = await PutAsync(_ligada, id, sessao, """{ "tetoCustoUsd": 2.50 }""");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            (corpo.GetProperty("tetoCustoUsd").GetDecimal(), corpo.GetProperty("padraoUsd").GetDecimal(), corpo.GetProperty("efetivoUsd").GetDecimal())
                .Should().Be((2.50m, 2.00m, 2.50m));

            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            var auditoria = (await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == id).ToListAsync(Ct))
                .Should().ContainSingle().Subject;
            (auditoria.Acao, auditoria.Entidade).Should().Be((AcaoAuditoria.Alterou, "Aplicacao"));
            auditoria.Depois.Should().Contain("2.5");
        }

        [Fact, DisplayName("Critério 1: corpo com null (ou sem o campo) restaura o padrão")]
        public async Task Mor_Restaura()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, $"sub-mor-teto-{Guid.NewGuid():N}", "mor");
            (await PutAsync(_ligada, id, sessao, """{ "tetoCustoUsd": 7 }""")).StatusCode.Should().Be(HttpStatusCode.OK);

            var resposta = await PutAsync(_ligada, id, sessao, "{}");

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            corpo.TryGetProperty("tetoCustoUsd", out _).Should().BeFalse();
            (corpo.GetProperty("padraoUsd").GetDecimal(), corpo.GetProperty("efetivoUsd").GetDecimal()).Should().Be((2.00m, 2.00m));
        }

        [Fact, DisplayName("Critério 1: valor fora da faixa responde 400 aplicacao.teto-invalido")]
        public async Task Invalido()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, $"sub-mor-teto-{Guid.NewGuid():N}", "mor");

            var resposta = await PutAsync(_ligada, id, sessao, """{ "tetoCustoUsd": 50.01 }""");

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.teto-invalido");
        }

        [Fact, DisplayName("Critério 4: com papel e sem ser MOR responde 403 acesso.papel-insuficiente; sem papel, 404")]
        public async Task Autorizacao()
        {
            var analista = $"sub-teto-analista-{Guid.NewGuid():N}";
            var id = await AplicacaoAsync((analista, PapelAplicacao.Analista));

            var comoAnalista = await PutAsync(_ligada, id, await SessaoAsync(_ligada, analista), """{ "tetoCustoUsd": 3 }""");
            var semPapel = await PutAsync(_ligada, id, await SessaoAsync(_ligada, $"sub-teto-estranho-{Guid.NewGuid():N}"), """{ "tetoCustoUsd": 3 }""");

            comoAnalista.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(comoAnalista)).Should().Be("acesso.papel-insuficiente");
            semPapel.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Critério 5: Features:X3 ausente responde 404 na rota")]
        public async Task FlagDesligada()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_desligada, $"sub-mor-teto-{Guid.NewGuid():N}", "mor");

            var resposta = await PutAsync(_desligada, id, sessao, """{ "tetoCustoUsd": 2.50 }""");

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Critério 11: dois PUT simultâneos com valores diferentes -> um 200 e um 409 aplicacao.conflito-de-gravacao")]
        public async Task Corrida_DoisPut()
        {
            var id = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, $"sub-mor-teto-{Guid.NewGuid():N}", "mor");
            _barreira.Armar(2);
            try
            {
                var a = PutAsync(_ligada, id, sessao, """{ "tetoCustoUsd": 2.50 }""");
                var b = PutAsync(_ligada, id, sessao, """{ "tetoCustoUsd": 4.00 }""");
                var respostas = await Task.WhenAll(a, b);

                respostas.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
                var perdedora = respostas.Single(r => r.StatusCode == HttpStatusCode.Conflict);
                (await CodigoDoErroAsync(perdedora)).Should().Be("aplicacao.conflito-de-gravacao");
            }
            finally
            {
                _barreira.Armar(1);
            }
        }
    }
}
