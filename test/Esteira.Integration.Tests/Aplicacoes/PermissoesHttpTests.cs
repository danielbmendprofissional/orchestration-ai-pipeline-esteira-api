using System.ComponentModel;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // Critérios 12, 13, 17 e 18 da A1 na borda HTTP: /permissoes com sessão simulada e flag Features:A1.
    [Trait("Fatia", "A1")]
    public sealed class PermissoesHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public PermissoesHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => b.UseSetting("Features:A1", "true"));
            _desligada = api.WithWebHostBuilder(b => b.UseSetting("Features:A1", "false"));
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private async Task<Guid> AplicacaoComPapelAsync(string sub, params PapelAplicacao[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);

            foreach (var papel in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));

            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        private static async Task<HttpResponseMessage> GetAsync(WebApplicationFactory<Program> api, string url, string? sessaoId)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);

            if (sessaoId is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessaoId}");

            return await cliente.SendAsync(pedido, Ct);
        }

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private static async Task ExigirPermissoesAsync(HttpResponseMessage resposta, bool editar, bool cadastro, bool politica, bool papeis)
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var corpo = await CorpoAsync(resposta);
            corpo.GetProperty("podeEditar").GetBoolean().Should().Be(editar);
            corpo.GetProperty("podeEditarCadastro").GetBoolean().Should().Be(cadastro);
            corpo.GetProperty("podeEditarPolitica").GetBoolean().Should().Be(politica);
            corpo.GetProperty("podeGerenciarPapeis").GetBoolean().Should().Be(papeis);
        }

        [Theory, DisplayName("Flag desligado responde 404 nas duas rotas de permissões")]
        [InlineData("/api/v1/aplicacoes/permissoes")]
        [InlineData("/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/permissoes")]
        public async Task FlagDesligado_404(string url)
        {
            var sessao = await SessaoAsync(_desligada, "sub-flag", "mor");

            var resposta = await GetAsync(_desligada, url, sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await resposta.Content.ReadAsStringAsync(Ct)).Should().NotContain("podeCriar").And.NotContain("podeEditar");
        }

        [Fact, DisplayName("Flag ausente na configuração também responde 404")]
        public async Task FlagAusente_404()
        {
            var api = _ligada.WithWebHostBuilder(b => b.UseSetting("Features:A1", ""));
            var sessao = await SessaoAsync(api, "sub-ausente", "mor");

            (await GetAsync(api, "/api/v1/aplicacoes/permissoes", sessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact, DisplayName("Permissões exigem sessão: sem cookie 401")]
        public async Task SemSessao_401()
        {
            (await GetAsync(_ligada, "/api/v1/aplicacoes/permissoes", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory, DisplayName("podeCriar é verdadeiro só para o MOR")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PodeCriar(bool mor)
        {
            var sessao = await SessaoAsync(_ligada, $"sub-criar-{mor}", mor ? ["mor"] : []);

            var resposta = await GetAsync(_ligada, "/api/v1/aplicacoes/permissoes", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await CorpoAsync(resposta)).GetProperty("podeCriar").GetBoolean().Should().Be(mor);
        }

        [Fact, DisplayName("MOR tem todas as permissões, mesmo sem papel")]
        public async Task Mor_TudoPermitido()
        {
            var id = await AplicacaoComPapelAsync("ninguem");
            var sessao = await SessaoAsync(_ligada, "sub-mor", "mor");

            await ExigirPermissoesAsync(await GetAsync(_ligada, $"/api/v1/aplicacoes/{id}/permissoes", sessao), true, true, true, true);
        }

        [Fact, DisplayName("Analista só edita cadastro")]
        public async Task Analista()
        {
            var sub = "sub-analista";
            var id = await AplicacaoComPapelAsync(sub, PapelAplicacao.Analista);
            var sessao = await SessaoAsync(_ligada, sub);

            await ExigirPermissoesAsync(await GetAsync(_ligada, $"/api/v1/aplicacoes/{id}/permissoes", sessao), false, true, false, false);
        }

        [Fact, DisplayName("Aprovador técnico só edita política")]
        public async Task AprovadorTecnico()
        {
            var sub = "sub-aprovador";
            var id = await AplicacaoComPapelAsync(sub, PapelAplicacao.AprovadorTecnico);
            var sessao = await SessaoAsync(_ligada, sub);

            await ExigirPermissoesAsync(await GetAsync(_ligada, $"/api/v1/aplicacoes/{id}/permissoes", sessao), false, false, true, false);
        }

        [Fact, DisplayName("Membro vê, mas não edita nada")]
        public async Task Membro()
        {
            var sub = "sub-membro";
            var id = await AplicacaoComPapelAsync(sub, PapelAplicacao.Membro);
            var sessao = await SessaoAsync(_ligada, sub);

            await ExigirPermissoesAsync(await GetAsync(_ligada, $"/api/v1/aplicacoes/{id}/permissoes", sessao), false, false, false, false);
        }

        [Fact, DisplayName("Sem papel e sem MOR: 404 aplicacao.nao-encontrada, nunca 403")]
        public async Task SemPapel_404()
        {
            var id = await AplicacaoComPapelAsync("outro");
            var sessao = await SessaoAsync(_ligada, "sub-sem-papel");

            var resposta = await GetAsync(_ligada, $"/api/v1/aplicacoes/{id}/permissoes", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("aplicacao.nao-encontrada");
        }

        [Fact, DisplayName("Aplicação inexistente tem a mesma resposta de sem acesso")]
        public async Task Inexistente_404()
        {
            var sessao = await SessaoAsync(_ligada, "sub-qualquer", "mor");

            var resposta = await GetAsync(_ligada, $"/api/v1/aplicacoes/{Guid.NewGuid()}/permissoes", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("aplicacao.nao-encontrada");
        }
    }
}
