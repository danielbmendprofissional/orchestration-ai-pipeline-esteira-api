using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Behavios;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Exceptions;
using NSubstitute;

namespace Esteira.Application.Tests.Common
{
    public class AutorizacaoBehaviorTests
    {
        public sealed class AlteracaoFluxo : IAlteraFluxoDeploy
        {
            public int IdFluxoDeploy => 1;
        }

        public sealed class OutraRequisicao { }

        private readonly IUsuarioAtual _usuario = Substitute.For<IUsuarioAtual>();
        private readonly IFluxoDeployQueryRepository _queryRepo = Substitute.For<IFluxoDeployQueryRepository>();

        public AutorizacaoBehaviorTests()
        {
            _queryRepo.ObterCriadorAsync(1, Arg.Any<CancellationToken>()).Returns("criador");
        }

        [Fact, DisplayName("Deve permitir que o criador altere o fluxo ignorando maiúsculas e minúsculas")]
        public async Task Criador_DevePermitir()
        {
            _usuario.Usuario.Returns("CRIADOR");

            var resposta = await new AutorizacaoBehavior<AlteracaoFluxo, string>(_usuario, _queryRepo)
                .Handle(new AlteracaoFluxo(), _ => Task.FromResult("ok"), CancellationToken.None);

            resposta.Should().Be("ok");
        }

        [Fact, DisplayName("Deve bloquear com 403 quando outro usuário tentar alterar o fluxo")]
        public async Task OutroUsuario_DeveBloquear()
        {
            _usuario.Usuario.Returns("outro");
            var executou = false;

            var act = () => new AutorizacaoBehavior<AlteracaoFluxo, string>(_usuario, _queryRepo)
                .Handle(new AlteracaoFluxo(), _ => { executou = true; return Task.FromResult("ok"); }, CancellationToken.None);

            (await act.Should().ThrowAsync<DomainException>().WithMessage("Somente o criador do fluxo de deploy pode alterá-lo."))
                .Which.StatusCode.Should().Be(403);
            executou.Should().BeFalse();
        }

        [Fact, DisplayName("Deve bloquear quando não houver usuário logado")]
        public async Task SemUsuario_DeveBloquear()
        {
            var act = () => new AutorizacaoBehavior<AlteracaoFluxo, string>(_usuario, _queryRepo)
                .Handle(new AlteracaoFluxo(), _ => Task.FromResult("ok"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact, DisplayName("Deve deixar o handler tratar o fluxo inexistente")]
        public async Task FluxoInexistente_DeveSeguir()
        {
            _queryRepo.ObterCriadorAsync(1, Arg.Any<CancellationToken>()).Returns((string)null);

            var resposta = await new AutorizacaoBehavior<AlteracaoFluxo, string>(_usuario, _queryRepo)
                .Handle(new AlteracaoFluxo(), _ => Task.FromResult("ok"), CancellationToken.None);

            resposta.Should().Be("ok");
        }

        [Fact, DisplayName("Não deve verificar o criador em requisições que não alteram fluxo")]
        public async Task OutraRequisicao_NaoDeveVerificar()
        {
            await new AutorizacaoBehavior<OutraRequisicao, string>(_usuario, _queryRepo)
                .Handle(new OutraRequisicao(), _ => Task.FromResult("ok"), CancellationToken.None);

            await _queryRepo.DidNotReceive().ObterCriadorAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }
}
