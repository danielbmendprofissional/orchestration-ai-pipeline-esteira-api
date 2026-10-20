using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaEspecificaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapasFluxoDeploy;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Commands
{
    public class EtapasFluxoDeployHandlersTests
    {
        private readonly IFluxoDeployRepository _repo = Substitute.For<IFluxoDeployRepository>();
        private readonly IEtapaRepository _etapaRepo = Substitute.For<IEtapaRepository>();

        public EtapasFluxoDeployHandlersTests()
        {
            _etapaRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Construtor.Catalogo);
        }

        private FluxoDeployEntity Retornar(FluxoDeployEntity fluxo, params FluxoDeployEntity[] doSistema)
        {
            _repo.GetByIdAsync(fluxo.Id, Arg.Any<CancellationToken>()).Returns(fluxo);
            _repo.ObterPorSistemaSomenteEtapasAsync(fluxo.Sistema, StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(new[] { fluxo }.Concat(doSistema).ToList());
            return fluxo;
        }

        [Fact, DisplayName("Deve voltar o fluxo para a etapa específica normalizando a sigla")]
        public async Task AlterarEtapaEspecifica_DeveVoltarParaEtapa()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "DEV", "QA", "HML"));
            fluxo.AvancarEtapa();
            var handler = new AlterarEtapaEspecificaFluxoDeployHandler(_repo, new FluxoDeployService());

            await handler.Handle(new AlterarEtapaEspecificaFluxoDeployCommand { Id = 1, Etapa = "dev" }, CancellationToken.None);

            fluxo.EtapaAtual().SglEtapa.Should().Be("DEV");
        }

        [Fact, DisplayName("Deve impedir voltar para etapa técnica quando há outro fluxo e o atual está em ambiente final")]
        public async Task AlterarEtapaEspecifica_OutroFluxoAberto_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "DEV", "HML");
            fluxo.AvancarEtapa();
            Retornar(fluxo, Construtor.Fluxo(2, "DEV"));
            var handler = new AlterarEtapaEspecificaFluxoDeployHandler(_repo, new FluxoDeployService());

            var act = () => handler.Handle(new AlterarEtapaEspecificaFluxoDeployCommand { Id = 1, Etapa = "DEV" }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa não pode ser selecionada*");
        }

        [Fact, DisplayName("Deve impedir voltar para uma etapa que o outro fluxo está executando")]
        public async Task AlterarEtapaEspecifica_EtapaEmExecucaoNoOutroFluxo_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "HML", "AAN", "PRD");
            fluxo.AvancarEtapa();
            Retornar(fluxo, Construtor.Fluxo(2, "HML"));
            var handler = new AlterarEtapaEspecificaFluxoDeployHandler(_repo, new FluxoDeployService());

            var act = () => handler.Handle(new AlterarEtapaEspecificaFluxoDeployCommand { Id = 1, Etapa = "HML" }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 2");
            fluxo.EtapaAtual().SglEtapa.Should().Be("AAN");
        }

        [Fact, DisplayName("Deve impedir reconfigurar as etapas quando a etapa que passa a executar está em execução no outro fluxo")]
        public async Task AlterarEtapas_NovaEtapaAtualNoOutroFluxo_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(2, "ABER", "DEV", "QA", "HML", "PRD");
            fluxo.AvancarEtapa();
            fluxo.AvancarEtapa();
            Retornar(fluxo, Construtor.Fluxo(1, "HML"));
            var handler = new AlterarEtapasFluxoDeployHandler(_repo, _etapaRepo, new FluxoDeployService());

            var act = () => handler.Handle(new AlterarEtapasFluxoDeployCommand { Id = 2, Etapas = new() { "ABER", "DEV", "HML", "PRD" } }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 1");
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao alterar etapa específica de fluxo inexistente")]
        public async Task AlterarEtapaEspecifica_FluxoInexistente_DeveLancarNotFound()
        {
            var handler = new AlterarEtapaEspecificaFluxoDeployHandler(_repo, new FluxoDeployService());

            var act = () => handler.Handle(new AlterarEtapaEspecificaFluxoDeployCommand { Id = 9, Etapa = "DEV" }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve reconfigurar as etapas do fluxo com o catálogo")]
        public async Task AlterarEtapas_DeveReconfigurar()
        {
            var fluxo = Retornar(Construtor.Fluxo(1, "ABER", "DEV", "QA", "HML", "PRD"));
            var handler = new AlterarEtapasFluxoDeployHandler(_repo, _etapaRepo, new FluxoDeployService());

            var resposta = await handler.Handle(new AlterarEtapasFluxoDeployCommand { Id = 1, Etapas = new() { "ABER", "DEV", "HML", "PRD" } }, CancellationToken.None);

            resposta.Message.Should().Be("Etapas reconfiguradas com sucesso!");
            fluxo.Etapas.Single(e => e.SglEtapa == "QA").Status.Should().Be(StatusEnum.CANCELADO);
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao reconfigurar etapas de fluxo inexistente")]
        public async Task AlterarEtapas_FluxoInexistente_DeveLancarNotFound()
        {
            var handler = new AlterarEtapasFluxoDeployHandler(_repo, _etapaRepo, new FluxoDeployService());

            var act = () => handler.Handle(new AlterarEtapasFluxoDeployCommand { Id = 9, Etapas = new() { "DEV" } }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }
    }
}
