using System.ComponentModel;
using System.Reflection;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Eventos;
using MassTransit;
using NSubstitute;

namespace Esteira.Application.Tests.Eventos
{
    // T1.4, critérios 7, 8, 9 (registro e reexecutar) e 10 (partida). Portas falsas.
    [Trait("Fatia", "T1")]
    public class RegistroDoWebhookTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private const string Segredo = "segredo-super-secreto-123";
        private const string UrlEsperada = "https://esteira.exemplo/api/webhooks/gitlab";
        private static readonly DateTime Agora = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

        private readonly IWebhookProjetoRepositorio _webhooks = Substitute.For<IWebhookProjetoRepositorio>();
        private readonly IProjetoRepositorio _projetos = Substitute.For<IProjetoRepositorio>();
        private readonly IAplicacaoRepositorio _apps = Substitute.For<IAplicacaoRepositorio>();
        private readonly IGitLabHooks _hooks = Substitute.For<IGitLabHooks>();
        private readonly IOpcoesDoWebhook _opcoes = Substitute.For<IOpcoesDoWebhook>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly TimeProvider _relogio = Substitute.For<TimeProvider>();
        private readonly WebhookProjetoEntity _webhook;

        public RegistroDoWebhookTests()
        {
            var app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;
            var projeto = ProjetoEntity.Criar(7, 501, "valuto/api", "api", Stack.Dotnet);
            DefinirId(projeto, 10);
            _webhook = WebhookProjetoEntity.Criar(10, Agora);
            _webhooks.ObterPorProjetoAsync(10, Arg.Any<CancellationToken>()).Returns(_webhook);
            _projetos.ObterPorIdAsync(10, Arg.Any<CancellationToken>()).Returns(projeto);
            _apps.ObterAsync(7, Arg.Any<CancellationToken>()).Returns(app);
            _opcoes.Segredo.Returns(Segredo);
            _opcoes.UrlPublica.Returns("https://esteira.exemplo/");
            _relogio.GetUtcNow().Returns(new DateTimeOffset(Agora));
            _hooks.ListarAsync("valuto", 501, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<HookGitLabDto>>.Sucesso([]));
            _hooks.CriarAsync("valuto", 501, Arg.Any<HookParaRegistrar>(), Arg.Any<CancellationToken>()).Returns(Result<long>.Sucesso(900));
            _hooks.AtualizarAsync("valuto", 501, Arg.Any<long>(), Arg.Any<HookParaRegistrar>(), Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _uow.SaveChangesAsync().Returns(1);
        }

        private static void DefinirId(EntityBase e, long id)
            => typeof(EntityBase).GetProperty(nameof(EntityBase.Id), BindingFlags.Instance | BindingFlags.Public)!.SetValue(e, id);

        private RegistradorDeWebhook Registrador() => new(_webhooks, _projetos, _apps, _hooks, _opcoes, _uow, _relogio);

        [Fact, DisplayName("Critério 7: sem hook na URL, cria com URL e segredo e grava o GitLabHookId (registrado)")]
        public async Task Cria()
        {
            await Registrador().RegistrarAsync(10, Ct);

            await _hooks.Received(1).CriarAsync("valuto", 501, Arg.Is<HookParaRegistrar>(h => h.Url == UrlEsperada && h.Segredo == Segredo), Arg.Any<CancellationToken>());
            await _hooks.DidNotReceiveWithAnyArgs().AtualizarAsync(default!, default, default, default!, default);
            (_webhook.Estado, _webhook.GitLabHookId, _webhook.CodigoErro).Should().Be((EstadoWebhookProjeto.Registrado, 900L, null));
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 8: hook com a mesma URL é atualizado (PUT), nunca duplicado")]
        public async Task Atualiza()
        {
            _hooks.ListarAsync("valuto", 501, Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<HookGitLabDto>>.Sucesso(
                [new HookGitLabDto(1, "https://outro/x"), new HookGitLabDto(77, UrlEsperada)]));

            await Registrador().RegistrarAsync(10, Ct);

            await _hooks.Received(1).AtualizarAsync("valuto", 501, 77, Arg.Is<HookParaRegistrar>(h => h.Url == UrlEsperada && h.Segredo == Segredo), Arg.Any<CancellationToken>());
            await _hooks.DidNotReceiveWithAnyArgs().CriarAsync(default!, default, default!, default);
            (_webhook.Estado, _webhook.GitLabHookId).Should().Be((EstadoWebhookProjeto.Registrado, 77L));
        }

        [Fact, DisplayName("Critério 8: repetir a mensagem com o webhook já registrado não chama o GitLab de novo")]
        public async Task Repetido()
        {
            await Registrador().RegistrarAsync(10, Ct);
            _hooks.ClearReceivedCalls();

            await Registrador().RegistrarAsync(10, Ct);

            _hooks.ReceivedCalls().Should().BeEmpty();
        }

        [Theory, DisplayName("Critério 9: falha do GitLab grava falhou com o código do webhook, sem lançar")]
        [InlineData("gitlab.indisponivel", "webhook.gitlab-indisponivel")]
        [InlineData("gitlab.bot-nao-configurado", "webhook.bot-nao-configurado")]
        [InlineData("gitlab.sem-permissao", "webhook.sem-permissao")]
        public async Task Falha(string codigoGitLab, string esperado)
        {
            _hooks.ListarAsync("valuto", 501, Arg.Any<CancellationToken>()).Returns(
                Result<IReadOnlyList<HookGitLabDto>>.Falha(new Erro(codigoGitLab, "x", TipoErro.Failure)));

            await Registrador().RegistrarAsync(10, Ct);

            (_webhook.Estado, _webhook.CodigoErro).Should().Be((EstadoWebhookProjeto.Falhou, esperado));
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 9: falha ao criar o hook também vira falhou")]
        public async Task FalhaAoCriar()
        {
            _hooks.CriarAsync("valuto", 501, Arg.Any<HookParaRegistrar>(), Arg.Any<CancellationToken>())
                .Returns(Result<long>.Falha(new Erro("gitlab.sem-permissao", "x", TipoErro.Forbidden)));

            await Registrador().RegistrarAsync(10, Ct);

            (_webhook.Estado, _webhook.CodigoErro).Should().Be((EstadoWebhookProjeto.Falhou, "webhook.sem-permissao"));
        }

        [Fact, DisplayName("Critério 9: segredo sem configuração vira falhou (bot-nao-configurado), sem chamar o GitLab")]
        public async Task SemConfiguracao()
        {
            _opcoes.Segredo.Returns((string?)null);

            await Registrador().RegistrarAsync(10, Ct);

            (_webhook.Estado, _webhook.CodigoErro).Should().Be((EstadoWebhookProjeto.Falhou, "webhook.bot-nao-configurado"));
            _hooks.ReceivedCalls().Should().BeEmpty();
        }

        [Fact, DisplayName("Webhook inexistente: no-op")]
        public async Task Inexistente()
        {
            await Registrador().RegistrarAsync(999, Ct);

            _hooks.ReceivedCalls().Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        // ---- Partida (10) ----

        [Fact, DisplayName("Critério 10: a partida lista os Projetos sem linha e enfileira cada um com linha pendente + mensagem")]
        public async Task Partida()
        {
            _webhooks.ListarProjetosAtivosSemWebhookAsync(Arg.Any<CancellationToken>()).Returns([10L, 11L]);
            WebhookProjetoEntity? criada = null;
            await _webhooks.AdicionarAsync(Arg.Do<WebhookProjetoEntity>(w => criada = w), Arg.Any<CancellationToken>());
            var partida = new PartidaDoWebhook(_webhooks, _uow, _publicador, _relogio);

            (await partida.ListarPendentesAsync(Ct)).Should().Equal(10, 11);
            (await partida.EnfileirarAsync(11, Ct)).Should().BeTrue();

            (criada!.ProjetoId, criada.Estado).Should().Be((11L, EstadoWebhookProjeto.Pendente));
            await _publicador.Received(1).Publish(Arg.Is<RegistrarWebhookPedido>(m => m.ProjetoId == 11), Arg.Any<CancellationToken>());
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 10: Projeto que já tem linha não é enfileirado de novo")]
        public async Task PartidaSemDuplicar()
        {
            var partida = new PartidaDoWebhook(_webhooks, _uow, _publicador, _relogio);

            (await partida.EnfileirarAsync(10, Ct)).Should().BeFalse();

            await _webhooks.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
            _publicador.ReceivedCalls().Should().BeEmpty();
        }

        // ---- Reexecutar (9) ----

        private ReexecutarWebhookHandler Reexecutar(out IAutorizacaoAplicacao autorizacao, out ProjetoEntity projeto)
        {
            autorizacao = Substitute.For<IAutorizacaoAplicacao>();
            projeto = ProjetoEntity.Criar(7, 501, "valuto/api", "api", Stack.Dotnet);
            DefinirId(projeto, 10);
            _projetos.ObterPorPublicIdAsync(projeto.PublicId, Arg.Any<CancellationToken>()).Returns(projeto);
            autorizacao.AutorizarAsync(7, OperacaoAplicacao.EditarAplicacao, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            return new ReexecutarWebhookHandler(autorizacao, _projetos, _webhooks, _publicador, _relogio);
        }

        [Fact, DisplayName("Critério 9: MOR reexecuta webhook falhou: volta a pendente e enfileira")]
        public async Task ReexecutaFalhou()
        {
            var handler = Reexecutar(out _, out var projeto);
            _webhook.Falhar("webhook.sem-permissao", Agora);

            var r = await handler.Handle(new ReexecutarWebhookCommand(projeto.PublicId), Ct);

            r.IsSuccess.Should().BeTrue();
            r.Value.Estado.Should().Be("pendente");
            _webhook.Estado.Should().Be(EstadoWebhookProjeto.Pendente);
            await _publicador.Received(1).Publish(Arg.Is<RegistrarWebhookPedido>(m => m.ProjetoId == 10), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("Critério 9: fora de falhou responde 409 webhook.nao-reexecutavel, sem enfileirar")]
        [InlineData("pendente")]
        [InlineData("registrado")]
        public async Task NaoReexecutavel(string estado)
        {
            var handler = Reexecutar(out _, out var projeto);
            if (estado == "registrado") _webhook.Registrar(5, Agora);

            var r = await handler.Handle(new ReexecutarWebhookCommand(projeto.PublicId), Ct);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("webhook.nao-reexecutavel", TipoErro.Conflict));
            _publicador.ReceivedCalls().Should().BeEmpty();
        }

        [Fact, DisplayName("Critério 9: só MOR (EditarAplicacao); sem permissão devolve o erro e não toca o webhook")]
        public async Task SoMor()
        {
            var handler = Reexecutar(out var autorizacao, out var projeto);
            _webhook.Falhar("webhook.sem-permissao", Agora);
            autorizacao.AutorizarAsync(7, OperacaoAplicacao.EditarAplicacao, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await handler.Handle(new ReexecutarWebhookCommand(projeto.PublicId), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            _webhook.Estado.Should().Be(EstadoWebhookProjeto.Falhou);
        }

        [Fact, DisplayName("Projeto inexistente responde projeto.nao-encontrado")]
        public async Task ProjetoInexistente()
        {
            var handler = Reexecutar(out _, out _);

            var r = await handler.Handle(new ReexecutarWebhookCommand(Guid.NewGuid()), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(ProjetoErros.NaoEncontrado);
        }
    }
}
