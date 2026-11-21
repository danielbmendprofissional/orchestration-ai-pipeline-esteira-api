using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Esteira.Application.Interfaces.Sessao;
using Esteira.Domain.Aplicacoes;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Aplicacoes
{
    // A1.5 na borda HTTP: busca de Pessoa (só MOR) e papéis da Aplicação (vincular/desvincular só MOR; ler com papel ou MOR),
    // critérios 13 e 16, com sessão simulada, flag Features:A1, auditoria na mesma transação e unicidade no banco.
    [Trait("Fatia", "A1")]
    public sealed class PapeisHttpTests : IClassFixture<ApiDeTeste>
    {
        private sealed record Aplic(Guid PublicId, long Id);

        private readonly InfraestruturaFixture _infra;
        private readonly WebApplicationFactory<Program> _ligada;
        private readonly WebApplicationFactory<Program> _desligada;

        public PapeisHttpTests(ApiDeTeste api, InfraestruturaFixture infra)
        {
            _infra = infra;
            _ligada = api.WithWebHostBuilder(b => b.UseSetting("Features:A1", "true"));
            _desligada = api.WithWebHostBuilder(b => b.UseSetting("Features:A1", "false"));
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<string> SessaoAsync(WebApplicationFactory<Program> api, string sub, params string[] perfis)
        {
            var tokens = ProvedorOidcFalso.Tokens("n", sub, perfis);
            var id = Guid.NewGuid().ToString("N");
            await api.Services.GetRequiredService<ISessaoStore>().CriarAsync(
                new SessaoUsuario(id, sub, tokens.AccessToken, tokens.RefreshToken, tokens.IdToken, DateTimeOffset.UtcNow.AddMinutes(5)),
                TimeSpan.FromMinutes(5));
            return id;
        }

        private static Task<HttpResponseMessage> EnviarAsync(
            WebApplicationFactory<Program> api, HttpMethod metodo, string url, string? sessao, object? corpo = null, (string, string)[]? headers = null)
        {
            var cliente = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            var pedido = new HttpRequestMessage(metodo, url);
            if (sessao is not null)
                pedido.Headers.Add("Cookie", $"esteira_session={sessao}");
            foreach (var (nome, valor) in headers ?? [])
                pedido.Headers.Add(nome, valor);
            if (corpo is not null)
                pedido.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
            return cliente.SendAsync(pedido, Ct);
        }

        private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string url, string? sessao, object? corpo = null, (string, string)[]? headers = null)
            => EnviarAsync(_ligada, metodo, url, sessao, corpo, headers);

        private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
            return doc.RootElement.Clone();
        }

        private static async Task<string> CodigoDoErroAsync(HttpResponseMessage resposta)
            => (await CorpoAsync(resposta)).GetProperty("errors")[0].GetProperty("code").GetString()!;

        private async Task<Aplic> AplicacaoAsync(params (string Sub, PapelAplicacao Papel)[] papeis)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var app = AplicacaoEntity.Criar("App", ContextoDoCadastro.SiglaUnica(), ContextoDoCadastro.IdUnico(), "grupo").Value;
            ctx.Add(app);
            await ctx.SaveChangesAsync(Ct);
            foreach (var (sub, papel) in papeis)
                ctx.Add(PapelAplicacaoEntity.Criar(app.Id, sub, papel));
            await ctx.SaveChangesAsync(Ct);
            return new Aplic(app.PublicId, app.Id);
        }

        private async Task PessoaAsync(string sub, string nome, string email)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            ctx.Add(PessoaEntity.Criar(sub, nome, email, DateTime.UtcNow));
            await ctx.SaveChangesAsync(Ct);
        }

        private async Task<PapelAplicacaoEntity> PapelNoBancoAsync(Aplic app, string sub, PapelAplicacao papel)
        {
            await using var ctx = ContextoDoCadastro.Novo(_infra);
            var entidade = PapelAplicacaoEntity.Criar(app.Id, sub, papel);
            ctx.Add(entidade);
            await ctx.SaveChangesAsync(Ct);
            return entidade;
        }

        private async Task<List<RegistroAuditoriaEntity>> AuditoriaAsync(Guid publicId)
        {
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            return await ctx.Set<RegistroAuditoriaEntity>().AsNoTracking().Where(x => x.EntidadePublicId == publicId).ToListAsync(Ct);
        }

        private async Task<int> QuantidadeDePapeisAsync(Aplic app)
        {
            await using var ctx = ContextoDoCadastro.NovoLeitura(_infra);
            return await ctx.Set<PapelAplicacaoEntity>().CountAsync(p => p.AplicacaoId == app.Id, Ct);
        }

        // ---- GET /pessoas ----

        [Fact, DisplayName("GET /pessoas: busca por nome ou e-mail (contém, sem diferenciar maiúsculas), ordenada por nome, só { sub, nome, email }")]
        public async Task Pessoas_Busca()
        {
            var marca = $"Pes{Guid.NewGuid():N}"[..10];
            await PessoaAsync($"sub-{marca}-b", $"Beto {marca}", "beto@x.com");
            await PessoaAsync($"sub-{marca}-a", "Ana", $"ANA.{marca}@x.com");
            await PessoaAsync($"sub-{marca}-z", "Fora da busca", "zed@x.com");
            var sessao = await SessaoAsync(_ligada, $"sub-mor-{marca}", "mor");

            var porNome = await Enviar(HttpMethod.Get, $"/api/v1/pessoas?busca={marca.ToUpperInvariant()}", sessao);

            porNome.StatusCode.Should().Be(HttpStatusCode.OK);
            var itens = await CorpoAsync(porNome);
            itens.EnumerateArray().Select(i => i.GetProperty("nome").GetString()).Should().Equal("Ana", $"Beto {marca}");
            itens[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("sub", "nome", "email");
            itens[0].GetProperty("sub").GetString().Should().Be($"sub-{marca}-a");
            itens[0].GetProperty("email").GetString().Should().Be($"ANA.{marca}@x.com");

            var soPorEmail = await CorpoAsync(await Enviar(HttpMethod.Get, $"/api/v1/pessoas?busca={marca.ToLowerInvariant()}%40x", sessao));
            soPorEmail.EnumerateArray().Select(i => i.GetProperty("sub").GetString()).Should().Equal($"sub-{marca}-a");
        }

        [Fact, DisplayName("GET /pessoas: paginação (ADR-0002) com headers X-* e ordenação por e-mail decrescente")]
        public async Task Pessoas_Paginada()
        {
            var marca = $"Pag{Guid.NewGuid():N}"[..10];
            foreach (var letra in new[] { "a", "b", "c" })
                await PessoaAsync($"sub-{marca}-{letra}", $"Pessoa {marca}", $"{letra}.{marca}@x.com");
            var sessao = await SessaoAsync(_ligada, $"sub-mor-{marca}", "mor");

            var pagina = await Enviar(HttpMethod.Get, $"/api/v1/pessoas?busca={marca}&sort=email&order=desc", sessao,
                headers: [("X-Page", "1"), ("X-Page-Size", "2")]);

            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            pagina.Headers.GetValues("X-Total-Count").Single().Should().Be("3");
            pagina.Headers.GetValues("X-Total-Pages").Single().Should().Be("2");
            pagina.Headers.GetValues("X-Page").Single().Should().Be("1");
            pagina.Headers.GetValues("X-Page-Size").Single().Should().Be("2");
            (await CorpoAsync(pagina)).EnumerateArray().Select(i => i.GetProperty("email").GetString())
                .Should().Equal($"c.{marca}@x.com", $"b.{marca}@x.com");
        }

        [Fact, DisplayName("Critério 13: GET /pessoas é só do MOR — quem tem papel numa Aplicação recebe 403 acesso.papel-insuficiente")]
        public async Task Pessoas_NaoMor_403()
        {
            var sub = $"sub-{Guid.NewGuid():N}";
            await AplicacaoAsync((sub, PapelAplicacao.Analista));
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Get, "/api/v1/pessoas?busca=a", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(resposta)).Should().Be("acesso.papel-insuficiente");
        }

        [Fact, DisplayName("GET /pessoas sem sessão responde 401")]
        public async Task Pessoas_SemSessao_401()
            => (await Enviar(HttpMethod.Get, "/api/v1/pessoas", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        [Fact, DisplayName("GET /pessoas: ordenação por campo inexistente responde 400")]
        public async Task Pessoas_SortInvalido_400()
        {
            var sessao = await SessaoAsync(_ligada, "sub-mor-sort-pes", "mor");

            (await Enviar(HttpMethod.Get, "/api/v1/pessoas?sort=senha", sessao)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ---- GET /aplicacoes/{id}/papeis ----

        [Fact, DisplayName("GET papéis: quem tem papel na Aplicação lê { id, usuario, nome, email, papel }; sub sem Pessoa vem sem nome/e-mail")]
        public async Task Papeis_Lista()
        {
            var marca = Guid.NewGuid().ToString("N");
            var leitor = $"sub-leitor-{marca}";
            await PessoaAsync($"sub-ana-{marca}", "Ana", $"ana-{marca}@x.com");
            var app = await AplicacaoAsync((leitor, PapelAplicacao.Membro), ($"sub-ana-{marca}", PapelAplicacao.Analista), ($"sub-sumiu-{marca}", PapelAplicacao.Publicador));
            var sessao = await SessaoAsync(_ligada, leitor);

            var resposta = await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            var itens = (await CorpoAsync(resposta)).EnumerateArray().ToList();
            itens.Should().HaveCount(3);
            var ana = itens.Single(i => i.GetProperty("usuario").GetString() == $"sub-ana-{marca}");
            (ana.GetProperty("nome").GetString(), ana.GetProperty("email").GetString(), ana.GetProperty("papel").GetString())
                .Should().Be(("Ana", $"ana-{marca}@x.com", "analista"));
            ana.GetProperty("id").GetGuid().Should().NotBeEmpty();
            var sumiu = itens.Single(i => i.GetProperty("usuario").GetString() == $"sub-sumiu-{marca}");
            sumiu.GetProperty("papel").GetString().Should().Be("publicador");
            (sumiu.TryGetProperty("nome", out var nome) ? nome.ValueKind : JsonValueKind.Null).Should().Be(JsonValueKind.Null);
            (sumiu.TryGetProperty("email", out var email) ? email.ValueKind : JsonValueKind.Null).Should().Be(JsonValueKind.Null);
        }

        [Fact, DisplayName("GET papéis: MOR lê sem ter papel; sem papel e sem MOR responde 404 aplicacao.nao-encontrada")]
        public async Task Papeis_Lista_Acesso()
        {
            var app = await AplicacaoAsync(($"sub-dono-{Guid.NewGuid():N}", PapelAplicacao.Analista));
            var mor = await SessaoAsync(_ligada, "sub-mor-lista-papeis", "mor");
            var estranho = await SessaoAsync(_ligada, "sub-estranho-lista-papeis");

            (await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/papeis", mor)).StatusCode.Should().Be(HttpStatusCode.OK);
            var negada = await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}/papeis", estranho);
            negada.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(negada)).Should().Be("aplicacao.nao-encontrada");
        }

        // ---- POST /aplicacoes/{id}/papeis ----

        [Fact, DisplayName("Critério 16: MOR vincula pessoa que já entrou — 201 com nome e e-mail, papel gravado e auditoria Criou")]
        public async Task Vincula_201()
        {
            var marca = Guid.NewGuid().ToString("N");
            await PessoaAsync($"sub-{marca}", "Bia", $"bia-{marca}@x.com");
            var app = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, $"sub-mor-{marca}", "mor");

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao,
                new { usuario = $"sub-{marca}", papel = "aprovador-tecnico" });

            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            var corpo = await CorpoAsync(resposta);
            (corpo.GetProperty("usuario").GetString(), corpo.GetProperty("nome").GetString(), corpo.GetProperty("email").GetString(), corpo.GetProperty("papel").GetString())
                .Should().Be(($"sub-{marca}", "Bia", $"bia-{marca}@x.com", "aprovador-tecnico"));
            var papelId = corpo.GetProperty("id").GetGuid();
            await using (var ctx = ContextoDoCadastro.NovoLeitura(_infra))
                (await ctx.Set<PapelAplicacaoEntity>().AsNoTracking().SingleAsync(p => p.PublicId == papelId, Ct))
                    .Should().Match<PapelAplicacaoEntity>(p => p.AplicacaoId == app.Id && p.Usuario == $"sub-{marca}" && p.Papel == PapelAplicacao.AprovadorTecnico);
            var registro = (await AuditoriaAsync(papelId)).Should().ContainSingle().Subject;
            (registro.Usuario, registro.Acao, registro.Entidade).Should().Be(($"sub-mor-{marca}", AcaoAuditoria.Criou, "PapelAplicacao"));
            registro.Antes.Should().BeNull();
            registro.Depois.Should().NotBeNull();
        }

        [Fact, DisplayName("Critério 16: pessoa que nunca entrou é recusada com 422 papel.pessoa-desconhecida; nada gravado nem auditado")]
        public async Task Vincula_PessoaDesconhecida_422()
        {
            var app = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-desconhecida", "mor");

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao,
                new { usuario = $"sub-nunca-entrou-{Guid.NewGuid():N}", papel = "membro" });

            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDoErroAsync(resposta)).Should().Be("papel.pessoa-desconhecida");
            (await QuantidadeDePapeisAsync(app)).Should().Be(0);
        }

        [Fact, DisplayName("Papel fora do vocabulário responde 400 papel.papel-invalido")]
        public async Task Vincula_PapelInvalido_400()
        {
            var marca = Guid.NewGuid().ToString("N");
            await PessoaAsync($"sub-{marca}", "Caio", $"caio-{marca}@x.com");
            var app = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-invalido", "mor");

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao, new { usuario = $"sub-{marca}", papel = "chefe" });

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await CodigoDoErroAsync(resposta)).Should().Be("papel.papel-invalido");
        }

        [Fact, DisplayName("Par (Aplicação, usuário, papel) repetido responde 409 papel.duplicado; outro papel da mesma pessoa é aceito")]
        public async Task Vincula_Duplicado_409()
        {
            var marca = Guid.NewGuid().ToString("N");
            await PessoaAsync($"sub-{marca}", "Dani", $"dani-{marca}@x.com");
            var app = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-mor-duplicado", "mor");
            var url = $"/api/v1/aplicacoes/{app.PublicId}/papeis";

            (await Enviar(HttpMethod.Post, url, sessao, new { usuario = $"sub-{marca}", papel = "analista" })).StatusCode.Should().Be(HttpStatusCode.Created);
            var repetido = await Enviar(HttpMethod.Post, url, sessao, new { usuario = $"sub-{marca}", papel = "analista" });
            var outroPapel = await Enviar(HttpMethod.Post, url, sessao, new { usuario = $"sub-{marca}", papel = "membro" });

            repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await CodigoDoErroAsync(repetido)).Should().Be("papel.duplicado");
            outroPapel.StatusCode.Should().Be(HttpStatusCode.Created);
            (await QuantidadeDePapeisAsync(app)).Should().Be(2);
        }

        [Fact, DisplayName("Unicidade no banco: o índice (Aplicação, Usuário, Papel) rejeita a duplicata mesmo sem a verificação prévia")]
        public async Task Unicidade_NoBanco()
        {
            var app = await AplicacaoAsync();
            await PapelNoBancoAsync(app, "sub-unico", PapelAplicacao.Membro);

            var act = () => PapelNoBancoAsync(app, "sub-unico", PapelAplicacao.Membro);

            await act.Should().ThrowAsync<DbUpdateException>();
        }

        [Fact, DisplayName("Critério 13: analista da Aplicação não vincula papel — 403 acesso.papel-insuficiente, nada gravado")]
        public async Task Vincula_NaoMor_403()
        {
            var marca = Guid.NewGuid().ToString("N");
            await PessoaAsync($"sub-alvo-{marca}", "Eva", $"eva-{marca}@x.com");
            var app = await AplicacaoAsync(($"sub-analista-{marca}", PapelAplicacao.Analista));
            var sessao = await SessaoAsync(_ligada, $"sub-analista-{marca}");

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao, new { usuario = $"sub-alvo-{marca}", papel = "membro" });

            resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(resposta)).Should().Be("acesso.papel-insuficiente");
            (await QuantidadeDePapeisAsync(app)).Should().Be(1);
        }

        [Fact, DisplayName("Sem papel na Aplicação e sem MOR: vincular responde 404 aplicacao.nao-encontrada")]
        public async Task Vincula_SemPapel_404()
        {
            var app = await AplicacaoAsync();
            var sessao = await SessaoAsync(_ligada, "sub-sem-papel-vincula");

            var resposta = await Enviar(HttpMethod.Post, $"/api/v1/aplicacoes/{app.PublicId}/papeis", sessao, new { usuario = "x", papel = "membro" });

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(resposta)).Should().Be("aplicacao.nao-encontrada");
        }

        // ---- DELETE /aplicacoes/{id}/papeis/{papelId} ----

        [Fact, DisplayName("MOR desvincula o papel: 204, removido do banco e auditoria Removeu na mesma transação")]
        public async Task Desvincula_204()
        {
            var app = await AplicacaoAsync();
            var papel = await PapelNoBancoAsync(app, "sub-sai", PapelAplicacao.Publicador);
            var sessao = await SessaoAsync(_ligada, "sub-mor-desvincula", "mor");

            var resposta = await Enviar(HttpMethod.Delete, $"/api/v1/aplicacoes/{app.PublicId}/papeis/{papel.PublicId}", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await QuantidadeDePapeisAsync(app)).Should().Be(0);
            var registro = (await AuditoriaAsync(papel.PublicId)).Should().ContainSingle().Subject;
            (registro.Usuario, registro.Acao, registro.Entidade).Should().Be(("sub-mor-desvincula", AcaoAuditoria.Removeu, "PapelAplicacao"));
            registro.Antes.Should().NotBeNull();
            registro.Depois.Should().BeNull();
        }

        [Fact, DisplayName("Desvincular papel de outra Aplicação ou inexistente responde 404 papel.nao-encontrado e não remove nada")]
        public async Task Desvincula_404()
        {
            var app = await AplicacaoAsync();
            var outra = await AplicacaoAsync();
            var daOutra = await PapelNoBancoAsync(outra, "sub-outra", PapelAplicacao.Membro);
            var sessao = await SessaoAsync(_ligada, "sub-mor-desvincula-404", "mor");

            var deOutra = await Enviar(HttpMethod.Delete, $"/api/v1/aplicacoes/{app.PublicId}/papeis/{daOutra.PublicId}", sessao);
            var inexistente = await Enviar(HttpMethod.Delete, $"/api/v1/aplicacoes/{app.PublicId}/papeis/{Guid.NewGuid()}", sessao);

            deOutra.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(deOutra)).Should().Be("papel.nao-encontrado");
            inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await CodigoDoErroAsync(inexistente)).Should().Be("papel.nao-encontrado");
            (await QuantidadeDePapeisAsync(outra)).Should().Be(1);
            (await AuditoriaAsync(daOutra.PublicId)).Should().BeEmpty();
        }

        [Fact, DisplayName("Critério 13: analista da Aplicação não desvincula papel — 403 e o papel continua")]
        public async Task Desvincula_NaoMor_403()
        {
            var sub = $"sub-analista-{Guid.NewGuid():N}";
            var app = await AplicacaoAsync((sub, PapelAplicacao.Analista));
            var alvo = await PapelNoBancoAsync(app, "sub-alvo", PapelAplicacao.Membro);
            var sessao = await SessaoAsync(_ligada, sub);

            var resposta = await Enviar(HttpMethod.Delete, $"/api/v1/aplicacoes/{app.PublicId}/papeis/{alvo.PublicId}", sessao);

            resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CodigoDoErroAsync(resposta)).Should().Be("acesso.papel-insuficiente");
            (await QuantidadeDePapeisAsync(app)).Should().Be(2);
        }

        // ---- GET /aplicacoes/{id} (detalhe) ----

        [Fact, DisplayName("Detalhe da Aplicação: papéis trazem nome e e-mail quando a Pessoa existe, sem quebrar id/usuario/papel")]
        public async Task Detalhe_PapeisComPessoa()
        {
            var marca = Guid.NewGuid().ToString("N");
            await PessoaAsync($"sub-fran-{marca}", "Fran", $"fran-{marca}@x.com");
            var app = await AplicacaoAsync(($"sub-fran-{marca}", PapelAplicacao.AprovadorNegocio), ($"sub-sem-pessoa-{marca}", PapelAplicacao.Membro));
            var sessao = await SessaoAsync(_ligada, "sub-mor-detalhe-papeis", "mor");

            var detalhe = await CorpoAsync(await Enviar(HttpMethod.Get, $"/api/v1/aplicacoes/{app.PublicId}", sessao));

            var papeis = detalhe.GetProperty("papeis").EnumerateArray().ToList();
            var fran = papeis.Single(p => p.GetProperty("usuario").GetString() == $"sub-fran-{marca}");
            (fran.GetProperty("nome").GetString(), fran.GetProperty("email").GetString(), fran.GetProperty("papel").GetString())
                .Should().Be(("Fran", $"fran-{marca}@x.com", "aprovador-negocio"));
            fran.GetProperty("id").GetGuid().Should().NotBeEmpty();
            var semPessoa = papeis.Single(p => p.GetProperty("usuario").GetString() == $"sub-sem-pessoa-{marca}");
            semPessoa.GetProperty("papel").GetString().Should().Be("membro");
            (semPessoa.TryGetProperty("nome", out var nome) ? nome.ValueKind : JsonValueKind.Null).Should().Be(JsonValueKind.Null);
        }

        // ---- flag ----

        [Theory, DisplayName("Critério 17: flag desligado responde 404 nas rotas de pessoas e de papéis")]
        [InlineData("GET", "/api/v1/pessoas?busca=a")]
        [InlineData("GET", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/papeis")]
        [InlineData("POST", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/papeis")]
        [InlineData("DELETE", "/api/v1/aplicacoes/6f9619ff-8b86-d011-b42d-00c04fc964ff/papeis/6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        public async Task FlagDesligado_404(string metodo, string url)
        {
            var sessao = await SessaoAsync(_desligada, "sub-flag-off-papeis", "mor");

            var resposta = await EnviarAsync(_desligada, new HttpMethod(metodo), url, sessao, metodo == "POST" ? new { usuario = "x", papel = "membro" } : null);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
