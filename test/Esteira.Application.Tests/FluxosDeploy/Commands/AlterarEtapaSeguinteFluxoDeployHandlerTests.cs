using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Events;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Commands
{
    public class AlterarEtapaSeguinteFluxoDeployHandlerTests
    {
        private readonly IFluxoDeployRepository _repo = Substitute.For<IFluxoDeployRepository>();
        private readonly IEtapaRepository _etapaRepo = Substitute.For<IEtapaRepository>();
        private readonly IEtapaRules _regraDev = Substitute.For<IEtapaRules>();
        private readonly IEtapaRules _regraPrd = Substitute.For<IEtapaRules>();
        private readonly AlterarEtapaSeguinteFluxoDeployHandler _handler;

        public AlterarEtapaSeguinteFluxoDeployHandlerTests()
        {
            _regraDev.Etapa.Returns(EtapaEnum.DEV);
            _regraPrd.Etapa.Returns(EtapaEnum.PRD);
            _etapaRepo.ObterCatalogoAsync(Arg.Any<CancellationToken>()).Returns(Construtor.Catalogo);

            _repo.ObterPorSistemaSomenteEtapasAsync(Arg.Any<string>(), StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(ci => _fluxosEmExecucao);

            _handler = new AlterarEtapaSeguinteFluxoDeployHandler(_repo, _etapaRepo, new[] { _regraDev, _regraPrd }, new FluxoDeployService());
        }

        private IReadOnlyList<FluxoDeployEntity> _fluxosEmExecucao = new List<FluxoDeployEntity>();

        private FluxoDeployEntity Retornar(FluxoDeployEntity fluxo)
        {
            _repo.GetByIdAsync(fluxo.Id, Arg.Any<CancellationToken>()).Returns(fluxo);
            return fluxo;
        }

        [Fact, DisplayName("Não deve avançar nem executar a regra quando a próxima etapa estiver em execução no outro fluxo")]
        public async Task Handle_ProximaEtapaNoOutroFluxo_NaoDeveAvancar()
        {
            var fluxo = Retornar(Construtor.Fluxo(2, "DEV", "HML"));
            _fluxosEmExecucao = new[] { Construtor.Fluxo(1, "HML"), fluxo };

            var act = () => _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 2 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 1");
            await _regraDev.DidNotReceive().RegrasEtapaAsync(Arg.Any<FluxoDeployEntity>(), Arg.Any<CancellationToken>());
            fluxo.EtapaAtual().SglEtapa.Should().Be("DEV");
        }

        [Fact, DisplayName("Deve executar a regra da etapa atual e avançar para a próxima")]
        public async Task Handle_DeveExecutarRegraEAvancar()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "DEV", "HML"));

            var resposta = await _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            await _regraDev.Received(1).RegrasEtapaAsync(fluxo, Arg.Any<CancellationToken>());
            await _regraPrd.DidNotReceive().RegrasEtapaAsync(Arg.Any<FluxoDeployEntity>(), Arg.Any<CancellationToken>());
            fluxo.EtapaAtual().SglEtapa.Should().Be("HML");
            fluxo.Status.Should().Be(StatusEnum.EXECUTANDO);
            resposta.Message.Should().Be("Etapa Desenvolvimento concluída com sucesso!");
        }

        [Fact, DisplayName("Deve concluir o fluxo ao avançar a etapa de produção")]
        public async Task Handle_EtapaPrd_DeveConcluirFluxo()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "PRD"));

            await _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            fluxo.Status.Should().Be(StatusEnum.CONCLUIDO);
            fluxo.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FluxoDeployConcluidoEvent>();
        }

        [Fact, DisplayName("Não deve avançar a etapa quando a regra falhar")]
        public async Task Handle_RegraFalha_NaoDeveAvancar()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "DEV", "HML"));
            _regraDev.RegrasEtapaAsync(fluxo, Arg.Any<CancellationToken>()).Returns(Task.FromException(new DomainException("falhou")));

            var act = () => _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("falhou");
            fluxo.EtapaAtual().SglEtapa.Should().Be("DEV");
        }

        [Fact, DisplayName("Deve lançar NotFoundException quando o fluxo não existir")]
        public async Task Handle_FluxoInexistente_DeveLancarNotFound()
        {
            var act = () => _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 99 }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo não estiver em execução")]
        public async Task Handle_FluxoConcluido_DeveLancarExcecao()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "DEV"));
            fluxo.Concluir();

            var act = () => _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Fluxo deploy '1' não está em execução");
        }

        [Fact, DisplayName("Deve lançar exceção quando não houver regra para a etapa atual")]
        public async Task Handle_SemRegraParaEtapa_DeveLancarExcecao()
        {
            Retornar(Construtor.Fluxo(1, "QA"));

            var act = () => _handler.Handle(new AlterarEtapaSeguinteFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não existem regras configuradas para a etapa QA");
        }
    }
}
