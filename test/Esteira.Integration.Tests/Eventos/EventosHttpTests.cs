using System.ComponentModel;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Api.Eventos;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Eventos
{
    // T1.2, critérios 11 e 12: GET /api/v1/eventos (SSE) na borda HTTP, com sessão simulada. Os intervalos (ping, duração
    // máxima, limite de conexões) entram por EventosOptions, então o teste usa valores curtos sem esperar 15 s nem 5 min.
    [Trait("Fatia", "T1")]
    public sealed class EventosHttpTests : IClassFixture<ApiDeTeste>
    {
        private readonly ApiDeTeste _api;
        private readonly InfraestruturaFixture _infra;
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public EventosHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _api = api;
            _infra = infra;
        }

        private WebApplicationFactory<Program> Com(EventosOptions opcoes) => _api.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton(opcoes)));

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private async Task<Guid> AplicacaoAsync(string sub)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var grupo = $"g{Guid.NewGuid():N}"[..10];
            var app = AplicacaoEntity.Criar($"ev{Guid.NewGuid():N}"[..8], ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), grupo).Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, PapelAplicacao.Membro));
            await ctx.SaveChangesAsync(Ct);
            return app.PublicId;
        }

        private static Task<HttpResponseMessage> AbrirAsync(WebApplicationFactory<Program> api, string? sessao, CancellationToken ct)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/eventos");
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            return cliente.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        // Lê linhas até achar uma que satisfaça o predicado (ou o fluxo acabar / estourar o prazo). null = não achou.
        private static async Task<string?> LinhaAsync(StreamReader leitor, Func<string, bool> procura, TimeSpan prazo)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            cts.CancelAfter(prazo);
            try
            {
                while (await leitor.ReadLineAsync(cts.Token) is { } linha)
                    if (procura(linha))
                        return linha;
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private static async Task PublicarAsync(WebApplicationFactory<Program> api, EventoDeTempoReal evento)
            => await api.Services.GetRequiredService<IPublicadorDeEventos>().PublicarAsync(evento, Ct);

        [Fact, DisplayName("11: sem sessão responde 401")]
        public async Task Sem_sessao_401()
        {
            var api = Com(new EventosOptions());

            using var resposta = await AbrirAsync(api, null, Ct);

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact, DisplayName("11: os padrões são ping a cada 15 s, 5 min de duração e 6 conexões por pessoa")]
        public void Padroes()
        {
            var o = new EventosOptions();

            o.IntervaloDePing.Should().Be(TimeSpan.FromSeconds(15));
            o.DuracaoMaxima.Should().Be(TimeSpan.FromMinutes(5));
            o.MaximoDeConexoesPorPessoa.Should().Be(6);
        }

        [Fact, DisplayName("11: abre text/event-stream sem cache nem buffering e entrega só eventos das Aplicações que a pessoa vê")]
        public async Task Entrega_so_o_que_a_pessoa_ve()
        {
            var api = Com(new EventosOptions { IntervaloDePing = TimeSpan.FromSeconds(30) });
            var sub = $"sub-ev-{Guid.NewGuid():N}";
            var minha = await AplicacaoAsync(sub);
            var alheia = await AplicacaoAsync($"outra-{Guid.NewGuid():N}");
            var sessao = await SessaoAsync(api, sub);

            using var resposta = await AbrirAsync(api, sessao, Ct);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resposta.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
            resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
            resposta.Headers.GetValues("X-Accel-Buffering").Should().Equal("no");

            using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(Ct));
            var daAlheia = new AcaoAtualizada(alheia, "T1", Guid.NewGuid(), "confirmada");
            var daMinha = new AcaoAtualizada(minha, "T1", Guid.NewGuid(), "falhou");
            await PublicarAsync(api, daAlheia);
            await PublicarAsync(api, daMinha);

            var dados = await LinhaAsync(leitor, l => l.StartsWith("data:", StringComparison.Ordinal), TimeSpan.FromSeconds(10));

            dados.Should().NotBeNull();
            using var doc = JsonDocument.Parse(dados!["data:".Length..]);
            doc.RootElement.GetProperty("aplicacaoId").GetGuid().Should().Be(minha);
            doc.RootElement.GetProperty("acaoId").GetGuid().Should().Be(daMinha.AcaoId);
            // o evento da Aplicação alheia nunca aparece: nenhuma outra linha de dados chega
            (await LinhaAsync(leitor, l => l.StartsWith("data:", StringComparison.Ordinal), TimeSpan.FromMilliseconds(600))).Should().BeNull();
        }

        [Fact, DisplayName("11: manda a linha de comentário ': ping' no intervalo configurado")]
        public async Task Manda_ping()
        {
            var api = Com(new EventosOptions { IntervaloDePing = TimeSpan.FromMilliseconds(150) });
            var sessao = await SessaoAsync(api, $"sub-ping-{Guid.NewGuid():N}");

            using var resposta = await AbrirAsync(api, sessao, Ct);
            using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(Ct));

            (await LinhaAsync(leitor, l => l == ": ping", TimeSpan.FromSeconds(5))).Should().Be(": ping");
        }

        [Fact, DisplayName("11: a conexão encerra sozinha ao fim da duração máxima")]
        public async Task Encerra_sozinha()
        {
            var api = Com(new EventosOptions { IntervaloDePing = TimeSpan.FromMilliseconds(100), DuracaoMaxima = TimeSpan.FromMilliseconds(700) });
            var sessao = await SessaoAsync(api, $"sub-fim-{Guid.NewGuid():N}");

            using var resposta = await AbrirAsync(api, sessao, Ct);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            using var leitor = new StreamReader(await resposta.Content.ReadAsStreamAsync(Ct));
            var inicio = DateTime.UtcNow;

            // nenhuma linha satisfaz o predicado: só retorna null quando o fluxo acaba (prazo bem acima da duração máxima)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            while (await leitor.ReadLineAsync(cts.Token) is not null) { }

            (DateTime.UtcNow - inicio).Should().BeLessThan(TimeSpan.FromSeconds(5));
            cts.IsCancellationRequested.Should().BeFalse("o servidor fechou o fluxo antes do prazo do teste");
        }

        [Fact, DisplayName("12: a 7ª conexão da mesma pessoa recebe 429 eventos.limite-conexoes; ao fechar uma, o espaço é liberado")]
        public async Task Limite_de_seis_conexoes()
        {
            var api = Com(new EventosOptions { IntervaloDePing = TimeSpan.FromSeconds(30) });
            var sub = $"sub-lim-{Guid.NewGuid():N}";
            var sessao = await SessaoAsync(api, sub);
            var abertas = new List<HttpResponseMessage>();
            try
            {
                for (var i = 0; i < 6; i++)
                {
                    var r = await AbrirAsync(api, sessao, Ct);
                    abertas.Add(r);
                    r.StatusCode.Should().Be(HttpStatusCode.OK);
                }

                using var setima = await AbrirAsync(api, sessao, Ct);
                setima.StatusCode.Should().Be((HttpStatusCode)429);
                using var corpo = JsonDocument.Parse(await setima.Content.ReadAsStringAsync(Ct));
                corpo.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("eventos.limite-conexoes");

                // outra pessoa não é afetada
                using var outra = await AbrirAsync(api, await SessaoAsync(api, $"{sub}-outra"), Ct);
                outra.StatusCode.Should().Be(HttpStatusCode.OK);

                abertas[0].Dispose();
                HttpStatusCode? depois = null;
                for (var i = 0; i < 50 && depois != HttpStatusCode.OK; i++)
                {
                    await Task.Delay(100, Ct);
                    var r = await AbrirAsync(api, sessao, Ct);
                    depois = r.StatusCode;
                    if (r.StatusCode == HttpStatusCode.OK)
                        abertas.Add(r);
                    else
                        r.Dispose();
                }

                depois.Should().Be(HttpStatusCode.OK);
            }
            finally
            {
                foreach (var r in abertas)
                    r.Dispose();
            }
        }
    }
}
