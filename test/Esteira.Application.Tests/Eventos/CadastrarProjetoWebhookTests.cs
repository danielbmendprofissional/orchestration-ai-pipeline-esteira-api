using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes.Commands.CadastrarProjeto;
using Esteira.Application.Eventos;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Tests.Aplicacoes;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Eventos;
using MassTransit;
using NSubstitute;

namespace Esteira.Application.Tests.Eventos
{
    // T1.4, critério 7: o cadastro do Projeto grava WebhookProjeto pendente e a mensagem do registro na mesma transação
    // (a linha e a mensagem entram antes do SaveChanges final, o único que persiste os dois juntos).
    [Trait("Fatia", "T1")]
    public class CadastrarProjetoWebhookTests : CadastroTestBase
    {
        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAplicacaoRepositorio _apps = Substitute.For<IAplicacaoRepositorio>();
        private readonly IGitLab _gitlab = Substitute.For<IGitLab>();
        private readonly IWebhookProjetoRepositorio _webhooks = Substitute.For<IWebhookProjetoRepositorio>();
        private readonly IPublishEndpoint _publicador = Substitute.For<IPublishEndpoint>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;

        public CadastrarProjetoWebhookTests()
        {
            _acesso.ResolverIdAsync(_app.PublicId, Arg.Any<CancellationToken>()).Returns(AplicacaoId);
            _apps.ObterAsync(AplicacaoId, Arg.Any<CancellationToken>()).Returns(_app);
            _gitlab.ListarProjetosDoGrupoAsync("valuto", 42, Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<ProjetoGitLabDto>>.Sucesso([new ProjetoGitLabDto(501, "valuto/api", "api", "main")]));
        }

        [Fact, DisplayName("Critério 7: cadastrar grava WebhookProjeto pendente e publica RegistrarWebhookPedido antes do último SaveChanges")]
        public async Task GravaPendenteEMensagem()
        {
            await Projetos.AdicionarAsync(Arg.Do<ProjetoEntity>(p => DefinirId(p, 77)), Arg.Any<CancellationToken>());
            WebhookProjetoEntity? criada = null;
            await _webhooks.AdicionarAsync(Arg.Do<WebhookProjetoEntity>(w => criada = w), Arg.Any<CancellationToken>());
            var ordem = new List<string>();
            Uow.SaveChangesAsync().Returns(_ => { ordem.Add("save"); return Task.FromResult(1); });
            _webhooks.When(w => w.AdicionarAsync(Arg.Any<WebhookProjetoEntity>(), Arg.Any<CancellationToken>())).Do(_ => ordem.Add("webhook"));
            _publicador.When(p => p.Publish(Arg.Any<RegistrarWebhookPedido>(), Arg.Any<CancellationToken>())).Do(_ => ordem.Add("mensagem"));

            var r = await new CadastrarProjetoHandler(_acesso, Autorizacao, _apps, Projetos, _gitlab, Uow, Auditoria, _webhooks, _publicador, TimeProvider.System)
                .Handle(new CadastrarProjetoCommand(_app.PublicId, 501, "dotnet"), Ct);

            r.IsSuccess.Should().BeTrue();
            (criada!.ProjetoId, criada.Estado).Should().Be((77L, EstadoWebhookProjeto.Pendente));
            await _publicador.Received(1).Publish(Arg.Is<RegistrarWebhookPedido>(m => m.ProjetoId == 77), Arg.Any<CancellationToken>());
            ordem.Should().Equal("save", "webhook", "mensagem", "save");
        }
    }
}
