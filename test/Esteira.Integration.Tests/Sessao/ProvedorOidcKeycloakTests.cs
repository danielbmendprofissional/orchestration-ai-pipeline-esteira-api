using System.Net;
using System.Text;
using AwesomeAssertions;
using Esteira.Api.Autenticacao;
using Esteira.Domain.Common.Resultados;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esteira.Integration.Tests.Sessao
{
    // Critérios 11 e 12 da B3 no adaptador de verdade: como o Keycloak recusa decide se a sessão cai (invalid_grant =
    // reuso) ou se só o provedor está fora (nada disso pode derrubar a sessão). Sem rede: o handler HTTP é de mentira.
    public class ProvedorOidcKeycloakTests
    {
        private const string Discovery = """
            {"authorization_endpoint":"https://kc.fake/auth","token_endpoint":"https://kc.fake/token","end_session_endpoint":"https://kc.fake/logout"}
            """;

        private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            public List<string> Corpos { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content is not null)
                    Corpos.Add(await request.Content.ReadAsStringAsync(cancellationToken));

                return responder(request);
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string corpo)
            => new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

        private static ProvedorOidcKeycloak Criar(HandlerFalso handler)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "https://kc.fake/realms/esteira",
                ["Keycloak:ClientId"] = "esteira-api",
                ["Keycloak:ClientSecret"] = "segredo-de-teste"
            }).Build();

            return new ProvedorOidcKeycloak(new HttpClient(handler), config, new MemoryCache(new MemoryCacheOptions()), NullLogger<ProvedorOidcKeycloak>.Instance);
        }

        private static HandlerFalso ComToken(HttpStatusCode status, string corpo)
            => new(r => r.RequestUri!.AbsolutePath.EndsWith("token") ? Json(status, corpo) : Json(HttpStatusCode.OK, Discovery));

        [Fact]
        public async Task Refresh_com_invalid_grant_vira_sessao_invalida()
        {
            var provedor = Criar(ComToken(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Token reuse detected"}"""));

            var resultado = await provedor.RenovarAsync("refresh-usado");

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors[0].Code.Should().Be("auth.sessao-invalida");
            resultado.Errors[0].Type.Should().Be(TipoErro.Unauthorized);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")]
        [InlineData(HttpStatusCode.Unauthorized, """{"error":"unauthorized_client"}""")]
        [InlineData(HttpStatusCode.InternalServerError, """<html>erro</html>""")]
        [InlineData(HttpStatusCode.ServiceUnavailable, "")]
        public async Task Refresh_recusado_por_outro_motivo_vira_provedor_indisponivel_sem_derrubar_a_sessao(HttpStatusCode status, string corpo)
        {
            var provedor = Criar(ComToken(status, corpo));

            var resultado = await provedor.RenovarAsync("refresh-bom");

            resultado.Errors[0].Code.Should().Be("auth.provedor-indisponivel");
            resultado.Errors[0].Type.Should().Be(TipoErro.Failure);
        }

        [Fact]
        public async Task Refresh_com_o_Keycloak_fora_do_ar_vira_provedor_indisponivel()
        {
            var provedor = Criar(new HandlerFalso(_ => throw new HttpRequestException("sem rota")));

            var resultado = await provedor.RenovarAsync("refresh-bom");

            resultado.Errors[0].Code.Should().Be("auth.provedor-indisponivel");
        }

        [Fact]
        public async Task Refresh_bem_sucedido_devolve_os_tokens_e_manda_o_client_secret_so_no_corpo()
        {
            var handler = ComToken(HttpStatusCode.OK, """{"access_token":"a","refresh_token":"r2","id_token":"i","expires_in":300,"refresh_expires_in":1800}""");

            var resultado = await Criar(handler).RenovarAsync("r1");

            resultado.Value.Should().BeEquivalentTo(new TokensOidc("a", "r2", "i", 300, 1800));
            handler.Corpos.Should().ContainSingle().Which.Should().Contain("grant_type=refresh_token").And.Contain("refresh_token=r1").And.Contain("client_secret=segredo-de-teste");
        }

        [Fact]
        public async Task Troca_de_code_recusada_vira_login_falhou_mesmo_com_invalid_grant()
        {
            var provedor = Criar(ComToken(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

            var resultado = await provedor.TrocarCodeAsync("code", "verifier", "http://localhost:4200/api/v1/auth/callback");

            resultado.Errors[0].Code.Should().Be("auth.login-falhou");
        }
    }
}
