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

        private sealed record Login(string State, string Nonce, string Challenge, Dictionary<string, string> Parametros, Uri Destino, string CookieDeLogin);

        private async Task<Login> IniciarLoginAsync(string? returnUrl = null, HttpClient? cliente = null)
        {
            var url = "/api/v1/auth/login" + (returnUrl is null ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
            var resposta = await (cliente ?? _cliente).GetAsync(url, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var destino = resposta.Headers.Location!;
            var parametros = QueryHelpers.ParseQuery(destino.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
            var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_login="));
            return new Login(parametros["state"], parametros["nonce"], parametros["code_challenge"], parametros, destino, cookie);
        }

        private static string DesafioS256(string verifier)
            => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private void ProgramarTrocaBemSucedida(string nonce, int refreshExpiresIn = 1800)
            => _provedor.ResponderTroca = _ => Esteira.Domain.Common.Resultados.Result<Esteira.Api.Autenticacao.TokensOidc>
                .Sucesso(ProvedorOidcFalso.Tokens(nonce, refreshExpiresIn: refreshExpiresIn));

        // Manda de volta o cookie de login que o navegador recebeu em /auth/login (HandleCookies está desligado).
        private Task<HttpResponseMessage> CallbackAsync(string query, Login? login = null, HttpClient? cliente = null)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/callback" + query);

            if (login is not null)
                pedido.Headers.Add("Cookie", login.CookieDeLogin.Split(";")[0]);

            return (cliente ?? _cliente).SendAsync(pedido, TestContext.Current.CancellationToken);
        }

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

            var resposta = await CallbackAsync($"?code=codigo-1&state={login.State}", login);

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

            var resposta = await CallbackAsync($"?code=c&state={login.State}", login, cliente);

            resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_session=")).ToLowerInvariant().Should().Contain("secure");
        }

        [Fact]
        public async Task Parametros_do_callback_vindos_do_navegador_nao_vao_crus_pro_log_nem_pro_Redis()
        {
            var login = await IniciarLoginAsync();
            var forjado = "FORJADO-" + Guid.NewGuid().ToString("N") + "%0AX-Falso:%201";

            await CallbackAsync($"?error={forjado}&state={login.State}", login);
            var resposta = await CallbackAsync($"?code=c&state={new string((char)97, 300)}", login);

            resposta.Headers.Location!.OriginalString.Should().Be("/");
            var log = _api.Services.GetService(typeof(LogDeTeste)) as LogDeTeste;
            var vazou = log!.Eventos.Where(e => e.RenderMessage().Contains("FORJADO-")).Select(e => e.MessageTemplate.Text + " :: " + e.RenderMessage()).ToArray();
            vazou.Should().BeEmpty();
        }

        public enum Falha { StateDesconhecido, StateReusado, ErroDoKeycloak, KeycloakRecusaOCode, NonceDiferente, SemCode }

        [Theory]
        [InlineData(Falha.StateDesconhecido)]
        [InlineData(Falha.StateReusado)]
        [InlineData(Falha.ErroDoKeycloak)]
        [InlineData(Falha.KeycloakRecusaOCode)]
        [InlineData(Falha.NonceDiferente)]
        [InlineData(Falha.SemCode)]
        public async Task Callback_recusado_volta_para_a_raiz_sem_sessao_e_sem_motivo_na_URL(Falha falha)
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
                    (await CallbackAsync($"?code=c&state={login.State}", login)).StatusCode.Should().Be(HttpStatusCode.Redirect);
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

            var resposta = await CallbackAsync(query, login);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
            // O cookie de login é apagado em todo callback; sessão é que não pode nascer.
            resposta.Headers.TryGetValues("Set-Cookie", out var cookies);
            (cookies ?? []).Should().NotContain(c => c.StartsWith("esteira_session="));
        }

        [Theory]
        [InlineData("https://evil.example/roubo")]
        [InlineData("//evil.example")]
        [InlineData("javascript:alert(1)")]
        public async Task ReturnUrl_que_nao_e_caminho_relativo_vira_a_raiz_depois_do_login(string returnUrl)
        {
            var login = await IniciarLoginAsync(returnUrl);
            ProgramarTrocaBemSucedida(login.Nonce);

            var resposta = await CallbackAsync($"?code=c&state={login.State}", login);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
        }

        [Fact]
        public async Task Login_amarra_o_state_ao_navegador_com_um_cookie_curto_HttpOnly_Lax()
        {
            var login = await IniciarLoginAsync();

            var cookie = login.CookieDeLogin.ToLowerInvariant();
            cookie.Should().StartWith($"esteira_login={login.State.ToLowerInvariant()}");
            cookie.Should().Contain("httponly").And.Contain("samesite=lax").And.Contain("path=/api/v1/auth").And.Contain("max-age=300");
            cookie.Should().NotContain("secure");
        }

        [Fact]
        public async Task Callback_bem_sucedido_apaga_o_cookie_de_login()
        {
            var login = await IniciarLoginAsync();
            ProgramarTrocaBemSucedida(login.Nonce);

            var resposta = await CallbackAsync($"?code=c&state={login.State}", login);

            resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_login=")).ToLowerInvariant()
                .Should().StartWith("esteira_login=;").And.Contain("expires=thu, 01 jan 1970");
        }

        public enum CookieDeLogin { Ausente, DeOutroLogin, Adulterado }

        [Theory]
        [InlineData(CookieDeLogin.Ausente)]
        [InlineData(CookieDeLogin.DeOutroLogin)]
        [InlineData(CookieDeLogin.Adulterado)]
        public async Task Callback_sem_o_cookie_de_login_do_mesmo_navegador_e_recusado_e_gasta_o_state(CookieDeLogin caso)
        {
            // Login CSRF: o atacante autentica na conta dele e leva a vítima a abrir o callback dele.
            var doAtacante = await IniciarLoginAsync();
            var daVitima = await IniciarLoginAsync();
            ProgramarTrocaBemSucedida(doAtacante.Nonce);
            var trocasAntes = _provedor.Trocas.Count;
            var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/callback?code=c&state={doAtacante.State}");

            switch (caso)
            {
                case CookieDeLogin.DeOutroLogin:
                    pedido.Headers.Add("Cookie", daVitima.CookieDeLogin.Split(";")[0]);
                    break;
                case CookieDeLogin.Adulterado:
                    pedido.Headers.Add("Cookie", "esteira_login=" + doAtacante.State + "x");
                    break;
            }

            var resposta = await _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
            resposta.Headers.GetValues("Set-Cookie").Should().NotContain(c => c.StartsWith("esteira_session="));
            _provedor.Trocas.Count.Should().Be(trocasAntes, "o code nem chega a ser trocado");

            // O state foi consumido: nem o dono dele, com o cookie certo, consegue usá-lo depois.
            var depois = await CallbackAsync($"?code=c&state={doAtacante.State}", doAtacante);
            depois.Headers.Location!.OriginalString.Should().Be("/");
            depois.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
            cookies!.Should().NotContain(c => c.StartsWith("esteira_session="));
        }
    }
}
