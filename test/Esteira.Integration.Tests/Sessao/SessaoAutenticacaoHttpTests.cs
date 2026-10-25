using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Integration.Tests.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Sessao
{
    // Critérios 6, 7, 8, 14 e 15 da B3: o esquema `Sessao` lê o cookie, busca a sessão no Redis e exige login por padrão.
    public sealed class SessaoAutenticacaoHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly ISessaoStore _sessoes;
        private readonly ProvedorOidcFalso _provedor;
        private readonly HttpClient _cliente;

        public SessaoAutenticacaoHttpTests(ApiDeTeste api)
        {
            _sessoes = api.Services.GetRequiredService<ISessaoStore>();
            _provedor = api.Services.GetRequiredService<ProvedorOidcFalso>();
            _cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        }

        private static string NovoId() => Guid.NewGuid().ToString("N");

        private async Task<string> CriarSessaoAsync(TimeSpan? duracao = null, DateTimeOffset? expiraEm = null, string sub = "user-1", string[]? perfis = null)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = NovoId();
            await _sessoes.CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, expiraEm ?? DateTimeOffset.UtcNow.AddMinutes(5)),
                duracao ?? TimeSpan.FromMinutes(5));
            return id;
        }

        private Task<HttpResponseMessage> GetAsync(string url, string? sessaoId = null)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, url);

            if (sessaoId is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessaoId}");

            return _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        }

        private static async Task DeveSer401ComCodeAsync(HttpResponseMessage resposta, string code)
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            resposta.Headers.Contains("WWW-Authenticate").Should().BeFalse("o desafio do BFF não é Bearer");
            using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var erro = corpo.RootElement.GetProperty("errors")[0];
            erro.GetProperty("code").GetString().Should().Be(code);
            erro.GetProperty("type").GetString().Should().Be("Unauthorized");
            erro.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Sem_cookie_responde_401_nao_autenticado_em_errors()
            => await DeveSer401ComCodeAsync(await GetAsync("/api/teste-protegido"), "auth.nao-autenticado");

        [Fact]
        public async Task Cookie_desconhecido_responde_401_nao_autenticado()
            => await DeveSer401ComCodeAsync(await GetAsync("/api/teste-protegido", "id-que-nunca-existiu"), "auth.nao-autenticado");

        [Fact]
        public async Task Sessao_expirada_no_Redis_responde_401_nao_autenticado()
        {
            var id = await CriarSessaoAsync(duracao: TimeSpan.FromMilliseconds(200));
            await Task.Delay(600, TestContext.Current.CancellationToken);

            await DeveSer401ComCodeAsync(await GetAsync("/api/teste-protegido", id), "auth.nao-autenticado");
        }

        [Fact]
        public async Task Access_token_vencido_responde_401_access_token_expired_sem_chamar_o_Keycloak()
        {
            var id = await CriarSessaoAsync(expiraEm: DateTimeOffset.UtcNow.AddSeconds(-5));
            var chamadasAntes = _provedor.Trocas.Count;

            await DeveSer401ComCodeAsync(await GetAsync("/api/teste-protegido", id), "auth.access-token-expired");

            _provedor.Trocas.Count.Should().Be(chamadasAntes);
        }

        [Fact]
        public async Task Endpoint_sem_AllowAnonymous_exige_sessao_e_aceita_uma_valida()
        {
            var id = await CriarSessaoAsync();

            (await GetAsync("/api/teste-protegido")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await GetAsync("/api/teste-protegido", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory]
        [InlineData("/api/v1/auth/login")]
        [InlineData("/api/v1/auth/callback")]
        [InlineData("/health")]
        [InlineData("/metrics")]
        public async Task Endpoints_anonimos_respondem_sem_sessao(string rota)
            => (await GetAsync(rota)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        private sealed record Eu(string Usuario, string Nome, string Email, string[] Perfis);

        [Fact]
        public async Task Me_devolve_usuario_nome_email_e_perfis_da_sessao()
        {
            var id = await CriarSessaoAsync(perfis: ["mor", "outro"]);

            var resposta = await GetAsync("/api/v1/usuarios/me", id);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var eu = await resposta.Content.ReadFromJsonAsync<Eu>(new JsonSerializerOptions(JsonSerializerDefaults.Web), TestContext.Current.CancellationToken);
            eu.Should().BeEquivalentTo(new Eu("dev", "Dev Teste", "dev@esteira.local", ["mor", "outro"]));
        }

        [Fact]
        public async Task Me_sem_sessao_responde_401()
            => await DeveSer401ComCodeAsync(await GetAsync("/api/v1/usuarios/me"), "auth.nao-autenticado");
    }
}
