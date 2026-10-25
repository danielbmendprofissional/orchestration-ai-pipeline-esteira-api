using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Sessao
{
    // O fluxo inteiro como um navegador o faz: o cliente guarda e reenvia cookies sozinho (HandleCookies), então
    // atributos como Path, SameSite e expiração são exercitados de verdade — os outros testes mandam o cookie na mão.
    public sealed class FluxoComCookieJarHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly ProvedorOidcFalso _provedor;
        private readonly HttpClient _navegador;

        public FluxoComCookieJarHttpTests(ApiDeTeste api)
        {
            _provedor = api.Services.GetRequiredService<ProvedorOidcFalso>();
            _navegador = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        }

        private sealed record Eu(string Usuario, string Nome, string Email, string[] Perfis);

        private async Task<(string State, string Nonce)> AbrirLoginAsync(string? returnUrl = null)
        {
            var url = "/api/v1/auth/login" + (returnUrl is null ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
            var resposta = await _navegador.GetAsync(url, TestContext.Current.CancellationToken);
            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var q = QueryHelpers.ParseQuery(resposta.Headers.Location!.Query);
            return (q["state"].ToString(), q["nonce"].ToString());
        }

        private async Task<HttpResponseMessage> VoltarDoKeycloakAsync(string state, string nonce, string[]? perfis = null)
        {
            _provedor.ResponderTroca = _ => Result<Esteira.Api.Autenticacao.TokensOidc>.Sucesso(ProvedorOidcFalso.Tokens(nonce, perfis: perfis));
            return await _navegador.GetAsync($"/api/v1/auth/callback?code=c&state={state}", TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Login_callback_e_me_funcionam_so_com_os_cookies_que_a_API_mandou()
        {
            var (state, nonce) = await AbrirLoginAsync("/entregas");

            var callback = await VoltarDoKeycloakAsync(state, nonce, perfis: ["mor"]);
            callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            callback.Headers.Location!.OriginalString.Should().Be("/entregas");

            var me = await _navegador.GetAsync("/api/v1/usuarios/me", TestContext.Current.CancellationToken);

            me.StatusCode.Should().Be(HttpStatusCode.OK);
            var eu = await me.Content.ReadFromJsonAsync<Eu>(new JsonSerializerOptions(JsonSerializerDefaults.Web), TestContext.Current.CancellationToken);
            eu!.Perfis.Should().Contain("mor");
        }

        [Fact]
        public async Task Logout_com_cookie_jar_derruba_a_sessao_e_o_me_seguinte_volta_401()
        {
            var (state, nonce) = await AbrirLoginAsync();
            await VoltarDoKeycloakAsync(state, nonce);
            (await _navegador.GetAsync("/api/v1/usuarios/me", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

            var logout = await _navegador.GetAsync("/api/v1/auth/logout", TestContext.Current.CancellationToken);
            logout.StatusCode.Should().Be(HttpStatusCode.Redirect);

            (await _navegador.GetAsync("/api/v1/usuarios/me", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Callback_com_state_de_um_login_antigo_nao_cria_sessao_e_o_me_continua_401()
        {
            // O que aconteceu na prática: a tela do Keycloak ficou aberta, um novo /auth/login foi iniciado em
            // outra aba e o callback antigo chegou depois. Só vale o state do último login deste navegador.
            var (antigo, nonceAntigo) = await AbrirLoginAsync();
            await AbrirLoginAsync();

            var callback = await VoltarDoKeycloakAsync(antigo, nonceAntigo);

            callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            callback.Headers.Location!.OriginalString.Should().Be("/");
            (await _navegador.GetAsync("/api/v1/usuarios/me", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
