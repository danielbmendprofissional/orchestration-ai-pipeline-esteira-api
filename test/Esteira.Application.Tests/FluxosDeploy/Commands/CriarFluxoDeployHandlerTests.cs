using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.Etapas.Services;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Commands
{
    public class CriarFluxoDeployHandlerTests
    {
        private readonly IFluxoDeployRepository _repo = Substitute.For<IFluxoDeployRepository>();
        private readonly IEtapaRepository _etapaRepo = Substitute.For<IEtapaRepository>();
        private readonly IConfiguracaoSistemaRepository _configRepo = Substitute.For<IConfiguracaoSistemaRepository>();
        private readonly CriarFluxoDeployHandler _handler;

        public CriarFluxoDeployHandlerTests()
        {
            _etapaRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Construtor.Catalogo);
            _repo.ObterPorSistemaSomenteEtapasAsync(Arg.Any<string>(), Arg.Any<StatusEnum>(), Arg.Any<CancellationToken>())
                .Returns(new List<FluxoDeployEntity>());
            _configRepo.ExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            _configRepo.GetBySistemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Construtor.Configuracao());

            _handler = new CriarFluxoDeployHandler(_repo, new FluxoDeployService(), _etapaRepo, new EtapaService(), _configRepo);
        }

        private static CriarFluxoDeployCommand Command(params string[] etapas)
            => new("sistema", "usuario", (etapas.Length == 0 ? new[] { "ABER", "DEV", "HML", "PRD" } : etapas).ToList());

        [Fact, DisplayName("Deve criar o fluxo com as etapas na ordem do catálogo e ordem sequencial")]
        public async Task Handle_DeveCriarFluxoComEtapasOrdenadas()
        {
            FluxoDeployEntity criado = null;
            await _repo.AddAsync(Arg.Do<FluxoDeployEntity>(f => criado = f), Arg.Any<CancellationToken>());

            var resposta = await _handler.Handle(Command("PRD", "dev", "Aber", "HML"), CancellationToken.None);

            resposta.Message.Should().Be("Fluxo deploy criado com sucesso");
            criado.Should().NotBeNull();
            criado.Sistema.Should().Be("SISTEMA");
            criado.Versao.Should().Be("v1");
            criado.Etapas.OrderBy(e => e.Ordem).Select(e => e.SglEtapa).Should().Equal("ABER", "DEV", "HML", "PRD");
            criado.Etapas.Select(e => e.Ordem).Should().BeEquivalentTo(new int?[] { 1, 2, 3, 4 });
            criado.EtapaAtual().SglEtapa.Should().Be("ABER");
        }

        [Fact, DisplayName("Deve criar a configuração do sistema quando ela não existir")]
        public async Task Handle_SemConfiguracao_DeveCriarConfiguracao()
        {
            _configRepo.ExisteAsync("SISTEMA", Arg.Any<CancellationToken>()).Returns(false);

            await _handler.Handle(Command(), CancellationToken.None);

            await _configRepo.Received(1).AddAsync(Arg.Is<ConfiguracaoSistemaEntity>(c => c.Sistema == "SISTEMA"), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Não deve criar a configuração do sistema quando ela já existir")]
        public async Task Handle_ComConfiguracao_NaoDeveCriarConfiguracao()
        {
            await _handler.Handle(Command(), CancellationToken.None);

            await _configRepo.DidNotReceive().AddAsync(Arg.Any<ConfiguracaoSistemaEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve usar a última versão do sistema quando não houver fluxo em execução")]
        public async Task Handle_SemFluxoAtual_DeveUsarUltimaVersaoDoSistema()
        {
            _repo.ObterUltimaVersaoSistema("SISTEMA", Arg.Any<CancellationToken>()).Returns("v4");

            await _handler.Handle(Command(), CancellationToken.None);

            await _repo.Received(1).AddAsync(Arg.Is<FluxoDeployEntity>(f => f.Versao == "v5"), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve incrementar a versão do fluxo em execução quando ele estiver em homologação")]
        public async Task Handle_ComFluxoAtualEmHml_DeveIncrementarVersaoDoFluxoAtual()
        {
            var atual = Construtor.Fluxo(1, "HML");
            atual.Atualizar("v7", "SISTEMA", "usuario");
            _repo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>()).Returns(new[] { atual });

            await _handler.Handle(Command(), CancellationToken.None);

            await _repo.Received(1).AddAsync(Arg.Is<FluxoDeployEntity>(f => f.Versao == "v8"), Arg.Any<CancellationToken>());
            await _repo.DidNotReceive().ObterUltimaVersaoSistema(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve lançar exceção quando já existirem dois fluxos em execução")]
        public async Task Handle_DoisFluxosEmExecucao_DeveLancarExcecao()
        {
            _repo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(new[] { Construtor.Fluxo(1, "HML"), Construtor.Fluxo(2, "DEV") });

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Já existem 2 fluxos em EXECUÇÃO para a aplicação SISTEMA");
            await _repo.DidNotReceive().AddAsync(Arg.Any<FluxoDeployEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo atual ainda não chegou em homologação")]
        public async Task Handle_FluxoAtualAntesDeHml_DeveLancarExcecao()
        {
            _repo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(new[] { Construtor.Fluxo(1, "DEV", "HML") });

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando faltar etapa obrigatória")]
        public async Task Handle_EtapaObrigatoriaAusente_DeveLancarExcecao()
        {
            var act = () => _handler.Handle(Command("ABER", "DEV"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("As etapas obrigatórias não foram informadas*");
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa não existir no catálogo")]
        public async Task Handle_EtapaInexistente_DeveLancarExcecao()
        {
            var act = () => _handler.Handle(Command("ABER", "DEV", "HML", "PRD", "XYZ"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Etapas inválidas ou inativas: XYZ");
        }
    }
}
