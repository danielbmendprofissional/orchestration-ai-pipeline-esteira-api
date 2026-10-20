using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Commands.CriarDeploy;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.DTOs.Request;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services;
using MassTransit;
using NSubstitute;

namespace Esteira.Application.Tests.Deploys.Commands
{
    public class CriarDeployHandlerTests
    {
        private readonly IPublishEndpoint _bus = Substitute.For<IPublishEndpoint>();
        private readonly IFluxoDeployRepository _fluxoRepo = Substitute.For<IFluxoDeployRepository>();
        private readonly IDeployRepository _deployRepo = Substitute.For<IDeployRepository>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly CriarDeployHandler _handler;

        public CriarDeployHandlerTests()
        {
            _fluxoRepo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(new[] { Construtor.Fluxo(1, "DEV", "HML") });
            _deployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(new List<DeployEntity>());

            _handler = new CriarDeployHandler(_bus, _fluxoRepo, _deployRepo, new FluxoDeployService(), _uow);
        }

        private static CriarDeployCommand Command(string url = null, ArtefatoAbstractDtoRequest artefatos = null, string tag = "v1.2.0", string branch = "dev", int idProjetoGit = 10)
            => new()
            {
                Sistema = "sistema",
                Branch = branch,
                Tag = tag,
                IdPipeline = 9,
                IdProjetoGit = idProjetoGit,
                NomeProjetoGit = "projeto",
                Linguagem = "java",
                FerramentaArtefatos = url is null ? "TRIVY" : "OWASP_ZAP",
                Url = url,
                ArtefatoDto = artefatos
            };

        [Fact, DisplayName("Deve criar o deploy, gravar para obter o id e publicar a mensagem SAST com os artefatos")]
        public async Task Handle_SemUrl_DeveCriarDeployEPublicarSast()
        {
            DeployEntity criado = null;
            await _deployRepo.AddAsync(Arg.Do<DeployEntity>(d => criado = d), Arg.Any<CancellationToken>());
            var trivy = new TrivyDtoRequest
            {
                Results = new() { new ResultTrivyDto { Vulnerabilities = new() { new VulnerabilityTrivyDto { Severity = "HIGH", PrimaryURL = "https://cve", References = new() } } } }
            };

            await _handler.Handle(Command(artefatos: trivy), CancellationToken.None);

            criado.Should().NotBeNull();
            criado.IdFluxoDeploy.Should().Be(1);
            criado.Branch.Should().Be("DEV");
            criado.VersaoAplicacao.Should().Be("v1");
            await _uow.Received(1).SaveChangesAsync();
            await _bus.Received(1).Publish(
                Arg.Is<CriarAnaliseArtefatosSastMessage>(m => m.Ferramenta == "TRIVY" && m.IdPipeline == 9 && m.Artefato.Single().Severidade == "HIGH"),
                Arg.Any<CancellationToken>());
            await _bus.DidNotReceive().Publish(Arg.Any<CriarAnaliseArtefatosDastMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve publicar a mensagem DAST quando a URL for informada")]
        public async Task Handle_ComUrl_DevePublicarDast()
        {
            await _handler.Handle(Command(url: "https://app.exemplo.com"), CancellationToken.None);

            await _bus.Received(1).Publish(
                Arg.Is<CriarAnaliseArtefatosDastMessage>(m => m.Url == "https://app.exemplo.com" && m.Ferramenta == "OWASP_ZAP"),
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve reaproveitar o deploy em execução da mesma branch e projeto sem gravar outro")]
        public async Task Handle_DeployExistente_DeveReaproveitar()
        {
            var existente = Construtor.Deploy(5, idFluxoDeploy: 1, branch: "DEV", idProjetoGit: 10, tag: "v1.0.0");
            _deployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(new[] { existente });

            await _handler.Handle(Command(), CancellationToken.None);

            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
            await _uow.DidNotReceive().SaveChangesAsync();
            await _bus.Received(1).Publish(Arg.Is<CriarAnaliseArtefatosSastMessage>(m => m.DeployId == 5), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve lançar exceção quando a tag for de outra versão do deploy em execução")]
        public async Task Handle_DeployExistenteOutraVersao_DeveLancarExcecao()
        {
            var existente = Construtor.Deploy(5, idFluxoDeploy: 1, branch: "DEV", idProjetoGit: 10, tag: "v1.0.0");
            _deployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(new[] { existente });

            var act = () => _handler.Handle(Command(tag: "v2.0.0"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>();
            await _bus.DidNotReceive().Publish(Arg.Any<CriarAnaliseArtefatosSastMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve lançar exceção quando não houver fluxo em execução para o sistema")]
        public async Task Handle_SemFluxo_DeveLancarExcecao()
        {
            _fluxoRepo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>()).Returns(new List<FluxoDeployEntity>());

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("É preciso ter um fluxo de deploy criado*");
        }

        private void UmFluxo(FluxoDeployEntity fluxo)
            => _fluxoRepo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(new[] { fluxo });

        [Fact, DisplayName("Deve rejeitar deploy em ambiente final cuja etapa ainda não foi iniciada")]
        public async Task Handle_EtapaNaoIniciada_DeveLancarExcecao()
        {
            UmFluxo(Construtor.Fluxo(1, "DEV", "HML"));

            var act = () => _handler.Handle(Command(branch: "hml"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não é possível executar pois a etapa HML não está em EXECUÇÃO*");
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
            await _bus.DidNotReceive().Publish(Arg.Any<CriarAnaliseArtefatosSastMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve rejeitar deploy em ambiente técnico quando nenhuma etapa técnica estiver em execução")]
        public async Task Handle_AmbientesTecnicosConcluidos_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "DEV", "HML");
            fluxo.AvancarEtapa();
            UmFluxo(fluxo);

            var act = () => _handler.Handle(Command(branch: "dev"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não é possível executar a ação pois nenhuma das etapas a seguir está em EXECUÇÃO*");
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve rejeitar deploy na branch de uma etapa técnica já concluída mesmo com outra técnica em execução")]
        public async Task Handle_EtapaTecnicaConcluida_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "DEV", "QA", "HML");
            fluxo.AvancarEtapa();
            UmFluxo(fluxo);

            var act = () => _handler.Handle(Command(branch: "dev"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não é possível executar pois a etapa DEV está CONCLUÍDA*");
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve rejeitar deploy em ambiente final já concluído")]
        public async Task Handle_AmbienteFinalConcluido_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "HML", "PRD");
            fluxo.AvancarEtapa();
            UmFluxo(fluxo);

            var act = () => _handler.Handle(Command(branch: "hml"), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>();
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
        }

        private void DoisFluxosDotnetEAngular(bool inverterOrdem, params DeployEntity[] extras)
        {
            var f1 = Construtor.Fluxo(1, "DEV", "HML", "PRD");
            f1.AvancarEtapa();
            var f2 = Construtor.Fluxo(2, "DEV", "HML", "PRD");
            _fluxoRepo.ObterPorSistemaSomenteEtapasAsync("SISTEMA", StatusEnum.EXECUTANDO, Arg.Any<CancellationToken>())
                .Returns(inverterOrdem ? new[] { f2, f1 } : new[] { f1, f2 });

            var deploys = new List<DeployEntity>
            {
                Construtor.Deploy(1, idFluxoDeploy: 1, branch: "DEV", idProjetoGit: 10, tag: "v1.0.0", status: StatusEnum.CONCLUIDO),
                Construtor.Deploy(2, idFluxoDeploy: 1, branch: "DEV", idProjetoGit: 20, tag: "v40.0.0", status: StatusEnum.CONCLUIDO),
                Construtor.Deploy(3, idFluxoDeploy: 2, branch: "DEV", idProjetoGit: 10, tag: "v2.0.0"),
                Construtor.Deploy(4, idFluxoDeploy: 2, branch: "DEV", idProjetoGit: 20, tag: "v41.0.0")
            };
            deploys.AddRange(extras);
            _deployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(deploys);
        }

        [Theory, DisplayName("Deve criar o deploy de HML no fluxo que está em HML com a versão do projeto nesse fluxo")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_DoisFluxos_DeployHml_DeveIrParaFluxoEmHml(bool inverterOrdem)
        {
            DoisFluxosDotnetEAngular(inverterOrdem);
            DeployEntity criado = null;
            await _deployRepo.AddAsync(Arg.Do<DeployEntity>(d => criado = d), Arg.Any<CancellationToken>());

            await _handler.Handle(Command(tag: "v40.1.0", branch: "hml", idProjetoGit: 20), CancellationToken.None);

            criado.IdFluxoDeploy.Should().Be(1);
            criado.Branch.Should().Be("HML");
        }

        [Theory, DisplayName("Deve reaproveitar o deploy de DEV do fluxo que está em DEV")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_DoisFluxos_DeployDev_DeveIrParaFluxoEmDev(bool inverterOrdem)
        {
            DoisFluxosDotnetEAngular(inverterOrdem);

            await _handler.Handle(Command(tag: "v41.2.0", branch: "dev", idProjetoGit: 20), CancellationToken.None);

            await _bus.Received(1).Publish(Arg.Is<CriarAnaliseArtefatosSastMessage>(m => m.DeployId == 4), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("Deve rejeitar deploy em HML com a versão do fluxo que está em DEV")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_DoisFluxos_DeployHmlComVersaoDoFluxoEmDev_DeveLancarExcecao(bool inverterOrdem)
        {
            DoisFluxosDotnetEAngular(inverterOrdem);

            var act = () => _handler.Handle(Command(tag: "v41.0.0", branch: "hml", idProjetoGit: 20), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Versão aplicação do fluxo de deploy está incorreta*");
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("Deve rejeitar deploy em HML de projeto novo com a versão do fluxo que está em DEV")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_DoisFluxos_ProjetoNovoEmHmlComVersaoDoFluxoEmDev_DeveLancarExcecao(bool inverterOrdem)
        {
            DoisFluxosDotnetEAngular(inverterOrdem, Construtor.Deploy(5, idFluxoDeploy: 2, branch: "DEV", idProjetoGit: 30, tag: "v5.0.0"));

            var act = () => _handler.Handle(Command(tag: "v5.0.0", branch: "hml", idProjetoGit: 30), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A versão da tag tem que ser menor que v5");
            await _deployRepo.DidNotReceive().AddAsync(Arg.Any<DeployEntity>(), Arg.Any<CancellationToken>());
        }
    }
}
