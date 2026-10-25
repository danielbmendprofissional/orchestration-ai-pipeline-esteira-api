using System.Net;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;

namespace Esteira.Integration.Tests.Sessao
{
    // Critérios 1–4 da B3: login e callback do BFF (ADR-0004) contra o Redis real e o Keycloak falso.
    public sealed class LoginCallbackHttpTests : IClassFixture<ApiDeTeste>, IAsyncLifetime
    {
        private const string RedirectUriEsperado = "http://localhost:4200/api/v1/auth/callback";

        private readonly ApiDeTeste _api;
        private readonly InfraestruturaFixture _infra;
        private readonly ProvedorOidcFalso _provedor;
        private readonly HttpClient _cliente;
        private ConnectionMultiplexer _admin = null!;

        public LoginCallbackHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
            _provedor = (ProvedorOidcFalso)api.Services.GetService(typeof(ProvedorOidcFalso))!;
            _cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        }

        public async ValueTask InitializeAsync()
        {
            _admin = await ConnectionMultiplexer.ConnectAsync(_infra.RedisAdminConnectionString);
            _provedor.ResponderTroca = _ => Esteira.Domain.Common.Resultados.Result<Esteira.Api.Autenticacao.TokensOidc>.Falha(
                new Esteira.Domain.Common.Resultados.Erro("auth.login-falhou", "recusado", Esteira.Domain.Common.Resultados.TipoErro.Unauthorized));
        }

        public async ValueTask DisposeAsync() => await _admin.DisposeAsync();

        private sealed record Login(string State, string Nonce, string Challenge, Dictionary<string, string> Parametros, Uri Destino);

        private async Task<Login> IniciarLoginAsync(string? returnUrl = null, HttpClient? cliente = null)
        {
            var url = "/api/v1/auth/login" + (returnUrl is null ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
            var resposta = await (cliente ?? _cliente).GetAsync(url, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var destino = resposta.Headers.Location!;
            var parametros = QueryHelpers.ParseQuery(destino.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
            return new Login(parametros["state"], parametros["nonce"], parametros["code_challenge"], parametros, destino);
        }

        private static string DesafioS256(string verifier)
            => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private void ProgramarTrocaBemSucedida(string nonce, int refreshExpiresIn = 1800)
            => _provedor.ResponderTroca = _ => Esteira.Domain.Common.Resultados.Result<Esteira.Api.Autenticacao.TokensOidc>
                .Sucesso(ProvedorOidcFalso.Tokens(nonce, refreshExpiresIn: refreshExpiresIn));

        private Task<HttpResponseMessage> CallbackAsync(string query, HttpClient? cliente = null)
            => (cliente ?? _cliente).GetAsync("/api/v1/auth/callback" + query, TestContext.Current.CancellationToken);

        [Fact]
        public async Task Login_redireciona_para_o_provedor_com_PKCE_state_nonce_e_guarda_o_contexto_no_Redis()
        {
            var login = await IniciarLoginAsync("/entregas");

            login.Destino.GetLeftPart(UriPartial.Path).Should().Be(ProvedorOidcFalso.Endpoints.Autorizacao.ToString());
            login.Parametros["response_type"].Should().Be("code");
            login.Parametros["client_id"].Should().Be("esteira-api");
            login.Parametros["code_challenge_method"].Should().Be("S256");
            login.Parametros["redirect_uri"].Should().Be(RedirectUriEsperado);
            login.Parametros["scope"].Should().Contain("openid");
            login.State.Should().NotBeNullOrWhiteSpace();
            login.Nonce.Should().NotBeNullOrWhiteSpace();
            login.Challenge.Should().NotBeNullOrWhiteSpace();

            var chave = $"{InfraestruturaFixture.PrefixoRedis}login:{login.State}";
            (await _admin.GetDatabase().KeyExistsAsync(chave)).Should().BeTrue();
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync(chave);
            ttl!.Value.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task Callback_valido_cria_a_sessao_seta_o_cookie_e_volta_para_o_returnUrl()
        {
            var login = await IniciarLoginAsync("/entregas/7?aba=analises");
            ProgramarTrocaBemSucedida(login.Nonce, refreshExpiresIn: 1800);

            var resposta = await CallbackAsync($"?code=codigo-1&state={login.State}");

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/entregas/7?aba=analises");

            var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_session="));
            cookie.ToLowerInvariant().Should().Contain("httponly").And.Contain("samesite=strict").And.Contain("path=/");
            cookie.ToLowerInvariant().Should().NotContain("secure");
            var sessaoId = cookie["esteira_session=".Length..].Split(';')[0];

            var chaveSessao = $"{InfraestruturaFixture.PrefixoRedis}sessao:{sessaoId}";
            var ttl = await _admin.GetDatabase().KeyTimeToLiveAsync(chaveSessao);
            ttl!.Value.Should().BeGreaterThan(TimeSpan.FromSeconds(1700)).And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(1800));

            // PKCE: o verifier enviado na troca bate com o challenge S256 do login; o redirect_uri é o mesmo.
            var troca = _provedor.Trocas.Last(t => t.Code == "codigo-1");
            DesafioS256(troca.Verifier).Should().Be(login.Challenge);
            troca.RedirectUri.Should().Be(RedirectUriEsperado);
        }

        [Fact]
        public async Task Cookie_sai_Secure_quando_a_API_exige_HTTPS()
        {
            using var apiHttps = _api.WithWebHostBuilder(b => b.UseSetting("Autenticacao:RequerHttps", "true"));
            var cliente = apiHttps.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var provedor = (ProvedorOidcFalso)apiHttps.Services.GetService(typeof(ProvedorOidcFalso))!;
            var login = await IniciarLoginAsync(cliente: cliente);
            provedor.ResponderTroca = _ => Esteira.Domain.Common.Resultados.Result<Esteira.Api.Autenticacao.TokensOidc>
                .Sucesso(ProvedorOidcFalso.Tokens(login.Nonce));

            var resposta = await CallbackAsync($"?code=c&state={login.State}", cliente);

            resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_session=")).ToLowerInvariant().Should().Contain("secure");
        }

        public enum Falha { StateDesconhecido, StateReusado, ErroDoKeycloak, KeycloakRecusaOCode, NonceDiferente, SemCode }

        [Theory]
        [InlineData(Falha.StateDesconhecido)]
        [InlineData(Falha.StateReusado)]
        [InlineData(Falha.ErroDoKeycloak)]
        [InlineData(Falha.KeycloakRecusaOCode)]
        [InlineData(Falha.NonceDiferente)]
        [InlineData(Falha.SemCode)]
        public async Task Callback_recusado_volta_para_a_raiz_sem_cookie_e_sem_motivo_na_URL(Falha falha)
        {
            var login = await IniciarLoginAsync("/entregas");
            string query;

            switch (falha)
            {
                case Falha.StateDesconhecido:
                    query = "?code=c&state=nunca-emitido";
                    break;
                case Falha.StateReusado:
                    ProgramarTrocaBemSucedida(login.Nonce);
                    (await CallbackAsync($"?code=c&state={login.State}")).StatusCode.Should().Be(HttpStatusCode.Redirect);
                    query = $"?code=c&state={login.State}";
                    break;
                case Falha.ErroDoKeycloak:
                    query = $"?error=access_denied&error_description=segredo&state={login.State}";
                    break;
                case Falha.KeycloakRecusaOCode:
                    query = $"?code=c&state={login.State}";
                    break;
                case Falha.NonceDiferente:
                    ProgramarTrocaBemSucedida("outro-nonce");
                    query = $"?code=c&state={login.State}";
                    break;
                default:
                    query = $"?state={login.State}";
                    break;
            }

            var resposta = await CallbackAsync(query);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
            resposta.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeFalse($"não pode haver cookie, veio: {string.Join(",", cookies ?? [])}");
        }

        [Theory]
        [InlineData("https://evil.example/roubo")]
        [InlineData("//evil.example")]
        [InlineData("javascript:alert(1)")]
        public async Task ReturnUrl_que_nao_e_caminho_relativo_vira_a_raiz_depois_do_login(string returnUrl)
        {
            var login = await IniciarLoginAsync(returnUrl);
            ProgramarTrocaBemSucedida(login.Nonce);

            var resposta = await CallbackAsync($"?code=c&state={login.State}");

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
        }
    }
}
