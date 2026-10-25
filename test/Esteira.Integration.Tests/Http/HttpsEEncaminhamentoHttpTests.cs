using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Esteira.Integration.Tests.Http
{
    // BFF atrás do proxy do ng serve (dev) e do Ingress (cluster): em dev a API não pode mandar o navegador para
    // outra origem (HTTPS); fora dele, o esquema e o IP vêm dos X-Forwarded-* do Ingress.
    public sealed class HttpsEEncaminhamentoHttpTests(InfraestruturaFixture infra)
    {
        private static readonly WebApplicationFactoryClientOptions SemRedirect = new() { AllowAutoRedirect = false };

        private static async Task<(string Esquema, string? Ip)> LerAsync(HttpResponseMessage resposta)
        {
            using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return (corpo.RootElement.GetProperty("esquema").GetString()!, corpo.RootElement.GetProperty("ip").GetString());
        }

        [Fact]
        public async Task Em_Development_a_API_nao_redireciona_para_HTTPS_mesmo_com_porta_HTTPS_conhecida()
        {
            // O perfil "https" do launchSettings (7239) já derrubou o login do ng serve para outra origem.
            using var api = new ApiEmAmbiente(infra, "Development", portaHttps: "7239");

            var resposta = await api.CreateClient(SemRedirect).GetAsync("/api/teste-encaminhado", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Fora_de_Development_o_X_Forwarded_Proto_https_do_Ingress_evita_o_redirect_e_vira_o_esquema()
        {
            using var api = new ApiEmAmbiente(infra, "Staging", portaHttps: "443");
            var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/teste-encaminhado");
            pedido.Headers.Add("X-Forwarded-Proto", "https");
            pedido.Headers.Add("X-Forwarded-For", "203.0.113.9");

            var resposta = await api.CreateClient(SemRedirect).SendAsync(pedido, TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var (esquema, ip) = await LerAsync(resposta);
            esquema.Should().Be("https");
            ip.Should().Be("203.0.113.9");
        }

        [Fact]
        public async Task Fora_de_Development_pedido_em_HTTP_puro_sem_o_cabecalho_do_Ingress_ainda_vai_para_HTTPS()
        {
            using var api = new ApiEmAmbiente(infra, "Staging", portaHttps: "443");

            var resposta = await api.CreateClient(SemRedirect).GetAsync("/api/teste-encaminhado", TestContext.Current.CancellationToken);

            resposta.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
            resposta.Headers.Location!.Scheme.Should().Be("https");
        }
    }
}
