using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Api.Autenticacao;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Common.Resultados;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Sessao
{
    // Critérios 9–13 da B3: refresh com trava e reuse detection, e logout, contra o Redis real e o Keycloak falso.
    public sealed class RefreshLogoutHttpTests : IClassFixture<ApiDeTeste>, IAsyncLifetime
    {
        private readonly ApiDeTeste _api;
        private readonly ISessaoStore _sessoes;
        private readonly ProvedorOidcFalso _provedor;
        private readonly HttpClient _cliente;

        public RefreshLogoutHttpTests(ApiDeTeste api)
        {
            _api = api;
            _sessoes = api.Services.GetRequiredService<ISessaoStore>();
            _provedor = api.Services.GetRequiredService<ProvedorOidcFalso>();
            _cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        }

        public ValueTask InitializeAsync()
        {
            _provedor.AtrasoNaRenovacao = TimeSpan.Zero;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<(string Id, TokensOidc Tokens)> CriarSessaoAsync(string sub = "user-1", string refresh = "refresh-antigo", DateTimeOffset? expiraEm = null)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub) with { RefreshToken = refresh };
            var id = Guid.NewGuid().ToString("N");
            await _sessoes.CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, expiraEm ?? DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(30));
            return (id, tokens);
        }

        private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, string? sessaoId)
        {
            var pedido = new HttpRequestMessage(metodo, url);

            if (sessaoId is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessaoId}");

            return _cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
        }

        private static string[] CookiesDeSessao(HttpResponseMessage resposta)
            => resposta.Headers.TryGetValues("Set-Cookie", out var valores)
                ? valores.Where(c => c.StartsWith("esteira_session=")).ToArray()
                : [];

        private static void DeveExpirarOCookie(HttpResponseMessage resposta)
        {
            var cookie = CookiesDeSessao(resposta).Should().ContainSingle().Subject;
            cookie.Should().StartWith("esteira_session=;");
            cookie.ToLowerInvariant().Should().Contain("expires=thu, 01 jan 1970");
        }

        private static async Task<string> CodeDoErroAsync(HttpResponseMessage resposta)
        {
            using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return corpo.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;
        }

        private void ProgramarRenovacaoBemSucedida(string novoAccess = "novo")
            => _provedor.ResponderRenovacao = _ => Result<TokensOidc>.Sucesso(
                ProvedorOidcFalso.Tokens("n") with { AccessToken = ProvedorOidcFalso.Jwt(new { sub = "user-1", preferred_username = novoAccess }), RefreshToken = "refresh-novo", ExpiresIn = 300, RefreshExpiresIn = 3600 });

        [Fact]
        public async Task Refresh_troca_o_refresh_token_guarda_os_novos_na_mesma_sessao_e_responde_204()
        {
            var (id, _) = await CriarSessaoAsync(refresh: "refresh-antigo", expiraEm: DateTimeOffset.UtcNow.AddSeconds(-5));
            ProgramarRenovacaoBemSucedida();

            var resposta = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id);

            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
            _provedor.Renovacoes.Should().Contain("refresh-antigo");
            var sessao = await _sessoes.ObterAsync(id);
            sessao.Should().NotBeNull();
            sessao!.RefreshToken.Should().Be("refresh-novo");
            sessao.ExpiraEm.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(4));
            CookiesDeSessao(resposta).Should().BeEmpty("o cookie opaco não muda no refresh");
        }

        [Fact]
        public async Task Refresh_paralelo_da_mesma_sessao_chama_o_Keycloak_uma_vez_so_e_ambos_terminam_em_204()
        {
            var (id, _) = await CriarSessaoAsync(refresh: "refresh-paralelo");
            ProgramarRenovacaoBemSucedida();
            _provedor.AtrasoNaRenovacao = TimeSpan.FromMilliseconds(500);

            var respostas = await Task.WhenAll(
                EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id),
                EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id));

            respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
            _provedor.Renovacoes.Count(t => t == "refresh-paralelo").Should().Be(1);
        }

        [Fact]
        public async Task Refresh_com_invalid_grant_revoga_todas_as_sessoes_do_usuario_expira_o_cookie_e_responde_401()
        {
            var (id1, _) = await CriarSessaoAsync(sub: "user-reuso");
            var (id2, _) = await CriarSessaoAsync(sub: "user-reuso");
            var (idOutro, _) = await CriarSessaoAsync(sub: "outro-usuario");
            _provedor.ResponderRenovacao = _ => Result<TokensOidc>.Falha(new Erro("auth.sessao-invalida", "recusado", TipoErro.Unauthorized));

            var resposta = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id1);

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await CodeDoErroAsync(resposta)).Should().Be("auth.sessao-invalida");
            DeveExpirarOCookie(resposta);
            (await _sessoes.ObterAsync(id1)).Should().BeNull();
            (await _sessoes.ObterAsync(id2)).Should().BeNull();
            (await _sessoes.ObterAsync(idOutro)).Should().NotBeNull();
        }

        [Fact]
        public async Task Refresh_com_Keycloak_indisponivel_devolve_o_erro_e_mantem_a_sessao_e_o_cookie()
        {
            var (id, tokens) = await CriarSessaoAsync(sub: "user-fora-do-ar", refresh: "refresh-fora");
            _provedor.ResponderRenovacao = _ => Result<TokensOidc>.Falha(new Erro("auth.provedor-indisponivel", "fora do ar", TipoErro.Failure));

            var resposta = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id);

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "TipoErro.Failure da ADR-0003");
            (await CodeDoErroAsync(resposta)).Should().Be("auth.provedor-indisponivel");
            CookiesDeSessao(resposta).Should().BeEmpty();
            (await _sessoes.ObterAsync(id))!.RefreshToken.Should().Be(tokens.RefreshToken);
        }

        [Fact]
        public async Task Refresh_sem_sessao_responde_401_nao_autenticado_e_nao_chama_o_Keycloak()
        {
            var antes = _provedor.Renovacoes.Count;

            var semCookie = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", null);
            var desconhecido = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", "nunca-existiu-nunca-existiu");

            foreach (var resposta in new[] { semCookie, desconhecido })
            {
                resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                (await CodeDoErroAsync(resposta)).Should().Be("auth.nao-autenticado");
            }

            _provedor.Renovacoes.Count.Should().Be(antes);
        }

        [Fact]
        public async Task Logout_apaga_a_sessao_expira_o_cookie_e_vai_para_o_end_session_com_id_token_hint()
        {
            var (id, tokens) = await CriarSessaoAsync(sub: "user-logout");

            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/auth/logout", id);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var destino = resposta.Headers.Location!;
            destino.GetLeftPart(UriPartial.Path).Should().Be(ProvedorOidcFalso.Endpoints.EndSession!.ToString());
            var parametros = QueryHelpers.ParseQuery(destino.Query);
            parametros["id_token_hint"].ToString().Should().Be(tokens.IdToken);
            parametros["post_logout_redirect_uri"].ToString().Should().Be("http://localhost:4200/");
            DeveExpirarOCookie(resposta);
            (await _sessoes.ObterAsync(id)).Should().BeNull();
        }

        [Fact]
        public void Chamada_ao_Keycloak_tem_timeout_abaixo_do_TTL_da_trava_de_refresh()
        {
            var fabrica = _api.Services.GetRequiredService<IHttpClientFactory>();

            var cliente = fabrica.CreateClient(nameof(IProvedorOidc));

            cliente.Timeout.Should().BeLessThan(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Logout_durante_o_refresh_nao_ressuscita_a_sessao()
        {
            var (id, _) = await CriarSessaoAsync(sub: "user-corrida");
            ProgramarRenovacaoBemSucedida();
            _provedor.AtrasoNaRenovacao = TimeSpan.FromMilliseconds(600);

            var refresh = EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", id);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await EnviarAsync(HttpMethod.Get, "/api/v1/auth/logout", id);
            var resposta = await refresh;

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await CodeDoErroAsync(resposta)).Should().Be("auth.nao-autenticado");
            (await _sessoes.ObterAsync(id)).Should().BeNull("a sessão encerrada não pode voltar com os tokens novos");
        }

        [Fact]
        public async Task Logout_nao_deixa_o_redirect_com_id_token_hint_em_cache_nem_vazar_por_referer()
        {
            var (id, _) = await CriarSessaoAsync(sub: "user-cache");

            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/auth/logout", id);

            resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
            resposta.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        }

        [Theory]
        [InlineData("a:b")]
        [InlineData("curto")]
        public async Task Cookie_malformado_e_tratado_como_sem_sessao_em_refresh_e_logout(string cookie)
        {
            var antes = _provedor.Renovacoes.Count;

            var refresh = await EnviarAsync(HttpMethod.Post, "/api/v1/auth/refresh", cookie);
            var logout = await EnviarAsync(HttpMethod.Get, "/api/v1/auth/logout", cookie);

            refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await CodeDoErroAsync(refresh)).Should().Be("auth.nao-autenticado");
            logout.Headers.Location!.OriginalString.Should().Be("/");
            _provedor.Renovacoes.Count.Should().Be(antes);
        }

        [Fact]
        public async Task Logout_sem_sessao_vai_direto_para_a_raiz()
        {
            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/auth/logout", null);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.Location!.OriginalString.Should().Be("/");
        }
    }
}
