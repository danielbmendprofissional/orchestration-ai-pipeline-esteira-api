using System.Text;
using AwesomeAssertions;
using Esteira.Api.Autenticacao;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Sessao
{
    // Critério 5 da B3 (ADR-0004): nenhum token do Keycloak sai da API — nem em corpo, header, cookie ou URL,
    // nem no log. Única exceção: o id_token_hint do redirect de logout, que é para o próprio Keycloak.
    public sealed class TokensNaoVazamHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly ApiDeTeste _api;
        private readonly ProvedorOidcFalso _provedor;
        private readonly LogDeTeste _log;
        private readonly HttpClient _cliente;

        public TokensNaoVazamHttpTests(ApiDeTeste api)
        {
            _api = api;
            _provedor = api.Services.GetRequiredService<ProvedorOidcFalso>();
            _log = api.Services.GetRequiredService<LogDeTeste>();
            _cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        }

        private static async Task<string> TextoDaRespostaAsync(HttpResponseMessage resposta)
        {
            var texto = new StringBuilder();
            texto.AppendLine(resposta.ToString());
            texto.AppendLine(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return texto.ToString();
        }

        [Fact]
        public async Task Nenhum_token_aparece_em_resposta_cookie_URL_ou_log_do_login_ao_logout()
        {
            var marcador = Guid.NewGuid().ToString("N");
            var sub = $"sub-{marcador}";
            var tokensDoLogin = ProvedorOidcFalso.Tokens("sera-trocado", sub) with { RefreshToken = $"RT-login-{marcador}" };
            var tokensDoRefresh = ProvedorOidcFalso.Tokens("n", sub) with { RefreshToken = $"RT-refresh-{marcador}", AccessToken = ProvedorOidcFalso.Jwt(new { sub, novo = marcador }) };

            var respostas = new List<string>();

            // Login e callback
            var login = await _cliente.GetAsync("/api/v1/auth/login?returnUrl=/entregas", TestContext.Current.CancellationToken);
            respostas.Add(await TextoDaRespostaAsync(login));
            var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
            tokensDoLogin = tokensDoLogin with { IdToken = ProvedorOidcFalso.Jwt(new { sub, nonce = query["nonce"].ToString() }) };
            _provedor.ResponderTroca = _ => Result<TokensOidc>.Sucesso(tokensDoLogin);

            var callback = await _cliente.GetAsync($"/api/v1/auth/callback?code=c-{marcador}&state={query["state"]}", TestContext.Current.CancellationToken);
            respostas.Add(await TextoDaRespostaAsync(callback));
            var cookie = callback.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_session=")).Split(';')[0];

            // Uso autenticado, refresh e uma falha de refresh (log do caminho de erro)
            async Task<HttpResponseMessage> ComSessaoAsync(HttpMethod metodo, string url)
            {
                var pedido = new HttpRequestMessage(metodo, url);
                pedido.Headers.Add("Cookie", cookie);
                return await _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
            }

            respostas.Add(await TextoDaRespostaAsync(await ComSessaoAsync(HttpMethod.Get, "/api/v1/usuarios/me")));

            _provedor.ResponderRenovacao = _ => Result<TokensOidc>.Sucesso(tokensDoRefresh);
            respostas.Add(await TextoDaRespostaAsync(await ComSessaoAsync(HttpMethod.Post, "/api/v1/auth/refresh")));

            _provedor.ResponderRenovacao = _ => Result<TokensOidc>.Falha(new Erro("auth.sessao-invalida", "recusado", TipoErro.Unauthorized));
            respostas.Add(await TextoDaRespostaAsync(await ComSessaoAsync(HttpMethod.Post, "/api/v1/auth/refresh")));

            // Logout: o id_token_hint no Location é o único lugar onde um token pode aparecer.
            var novoLogin = await _cliente.GetAsync("/api/v1/auth/login", TestContext.Current.CancellationToken);
            var q2 = QueryHelpers.ParseQuery(novoLogin.Headers.Location!.Query);
            var tokensDoSegundoLogin = tokensDoLogin with { IdToken = ProvedorOidcFalso.Jwt(new { sub, nonce = q2["nonce"].ToString() }), RefreshToken = $"RT-login2-{marcador}" };
            _provedor.ResponderTroca = _ => Result<TokensOidc>.Sucesso(tokensDoSegundoLogin);
            var cb2 = await _cliente.GetAsync($"/api/v1/auth/callback?code=c2&state={q2["state"]}", TestContext.Current.CancellationToken);
            cookie = cb2.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_session=")).Split(';')[0];
            var logout = await ComSessaoAsync(HttpMethod.Get, "/api/v1/auth/logout");
            logout.Headers.Location!.Query.Should().Contain("id_token_hint");
            logout.Headers.Remove("Location");
            respostas.Add(await TextoDaRespostaAsync(logout));

            var log = string.Join("\n", _log.Eventos.Select(e => $"{e.RenderMessage()} {string.Join(" ", e.Properties.Select(p => p.Value.ToString()))} {e.Exception}"));

            var segredos = new[] { tokensDoLogin, tokensDoRefresh, tokensDoSegundoLogin }
                .SelectMany(t => new[] { t.AccessToken, t.RefreshToken, t.IdToken })
                .Distinct()
                .ToArray();

            foreach (var segredo in segredos)
            {
                respostas.Should().NotContain(r => r.Contains(segredo), $"o token {segredo[..12]}… não pode sair em resposta");
                log.Should().NotContain(segredo, $"o token {segredo[..12]}… não pode ir pro log");
            }
        }
    }
}
