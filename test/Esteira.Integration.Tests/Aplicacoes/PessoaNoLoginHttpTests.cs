using System.ComponentModel;
using System.Net;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Api.Autenticacao;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // Critério 16 da A1 (registro): o callback da B3 grava/atualiza Pessoa; falha ao gravar não derruba o login.
    [Trait("Fatia", "A1")]
    public sealed class PessoaNoLoginHttpTests(ApiDeTeste api, InfraestruturaFixture infra) : IClassFixture<ApiDeTeste>
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private sealed class RegistradorQueFalha : IRegistradorDePessoa
        {
            public Task RegistrarAsync(string sub, string nome, string email, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("banco fora do ar");
        }

        private static async Task<HttpResponseMessage> LoginCompletoAsync(WebApplicationFactory<Program> fabrica, string sub)
        {
            var provedor = fabrica.Services.GetRequiredService<ProvedorOidcFalso>();
            var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

            var inicio = await cliente.GetAsync("/api/v1/auth/login", Ct);
            var state = QueryHelpers.ParseQuery(inicio.Headers.Location!.Query)["state"].ToString();
            var nonce = QueryHelpers.ParseQuery(inicio.Headers.Location!.Query)["nonce"].ToString();
            var cookie = inicio.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("esteira_login=")).Split(";")[0];
            provedor.ResponderTroca = _ => Result<TokensOidc>.Sucesso(ProvedorOidcFalso.Tokens(nonce, sub));

            var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/callback?code=c&state={state}");
            pedido.Headers.Add("Cookie", cookie);
            return await cliente.SendAsync(pedido, Ct);
        }

        private async Task<PessoaEntity?> PessoaAsync(string sub)
        {
            await using var ctx = ContextoDoCadastro.NovoLeitura(infra);
            return await ctx.Set<PessoaEntity>().SingleOrDefaultAsync(p => p.Sub == sub, Ct);
        }

        [Fact, DisplayName("Callback valido grava a Pessoa com sub, nome, e-mail e UltimoLoginEm UTC")]
        public async Task Callback_GravaPessoa()
        {
            var sub = $"sub-{Guid.NewGuid():N}";
            var antes = DateTime.UtcNow.AddSeconds(-5);

            var resposta = await LoginCompletoAsync(api, sub);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var pessoa = await PessoaAsync(sub);
            pessoa.Should().NotBeNull();
            pessoa!.Nome.Should().Be("Dev Teste");
            pessoa.Email.Should().Be("dev@esteira.local");
            pessoa.UltimoLoginEm.Should().BeAfter(antes);
        }

        [Fact, DisplayName("Segundo login atualiza a mesma Pessoa, sem duplicar")]
        public async Task SegundoLogin_Atualiza()
        {
            var sub = $"sub-{Guid.NewGuid():N}";
            await LoginCompletoAsync(api, sub);
            var primeiro = (await PessoaAsync(sub))!.UltimoLoginEm;

            await LoginCompletoAsync(api, sub);

            await using var ctx = ContextoDoCadastro.NovoLeitura(infra);
            (await ctx.Set<PessoaEntity>().CountAsync(p => p.Sub == sub, Ct)).Should().Be(1);
            (await PessoaAsync(sub))!.UltimoLoginEm.Should().BeOnOrAfter(primeiro);
        }

        [Fact, DisplayName("Falha ao gravar a Pessoa não derruba o login: cookie de sessão sai e nada vaza no log")]
        public async Task FalhaAoGravar_LoginSegue()
        {
            var falha = api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IRegistradorDePessoa>();
                s.AddScoped<IRegistradorDePessoa, RegistradorQueFalha>();
            }));
            var sub = $"sub-{Guid.NewGuid():N}";

            var resposta = await LoginCompletoAsync(falha, sub);

            resposta.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resposta.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("esteira_session="));
            var log = falha.Services.GetRequiredService<LogDeTeste>();
            var textos = log.Eventos.Select(e => e.RenderMessage()).ToArray();
            textos.Should().Contain(t => t.Contains("Pessoa"));
            textos.Should().NotContain(t => t.Contains("dev@esteira.local") || t.Contains("refresh-token-secreto"));
        }
    }
}
