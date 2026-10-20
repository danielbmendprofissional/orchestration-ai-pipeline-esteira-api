using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Commands.IniciarAnaliseDinamica;
using Esteira.Application.Deploys.Commands.RegistrarScanAnalise;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Interfaces;
using NSubstitute;

namespace Esteira.Application.Tests.Deploys.Commands
{
    public class AnaliseDinamicaHandlersTests
    {
        private const string Url = "https://app.exemplo.com";

        private readonly IDeployRepository _deployRepo = Substitute.For<IDeployRepository>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private static IniciarAnaliseDinamicaCommand Command()
            => new() { DeployId = 1, Ferramenta = "OWASP_ZAP", IdPipeline = 9, Tag = "v1.0.0", Url = Url };

        [Fact, DisplayName("Deve ignorar a análise quando já existir scan em execução na mesma URL")]
        public async Task Iniciar_UrlEmExecucao_DeveIgnorar()
        {
            _deployRepo.ExisteAnaliseDinamicaEmExecucaoNaUrlAsync(Url, Arg.Any<CancellationToken>()).Returns(true);

            var resultado = await new IniciarAnaliseDinamicaHandler(_deployRepo, _uow).Handle(Command(), CancellationToken.None);

            resultado.Should().BeNull();
            await _deployRepo.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("Deve registrar a análise em execução com a URL e gravar antes de chamar o scanner DAST")]
        public async Task Iniciar_UrlLivre_DeveRegistrarEGravar()
        {
            var deploy = Construtor.Deploy(1);
            _deployRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(deploy);

            var resultado = await new IniciarAnaliseDinamicaHandler(_deployRepo, _uow).Handle(Command(), CancellationToken.None);

            resultado.Should().NotBeNull();
            var analise = deploy.Analises.Should().ContainSingle().Subject;
            analise.UrlAlvo.Should().Be(Url);
            analise.Status.Should().Be(StatusEnum.EXECUTANDO);
            analise.ScanId.Should().BeNull();
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao iniciar análise de deploy inexistente")]
        public async Task Iniciar_DeployInexistente_DeveLancarNotFound()
        {
            var act = () => new IniciarAnaliseDinamicaHandler(_deployRepo, _uow).Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve registrar o scan retornado pelo scanner DAST")]
        public async Task RegistrarScan_ComScan_DeveAtribuir()
        {
            var deploy = Construtor.Deploy(1);
            var analise = deploy.IniciarAnaliseDinamica("OWASP_ZAP", 9, "v1.0.0", Url);
            _deployRepo.ObterPorAnaliseAsync(analise.Id, Arg.Any<CancellationToken>()).Returns(deploy);

            await new RegistrarScanAnaliseHandler(_deployRepo).Handle(new RegistrarScanAnaliseCommand { IdAnalise = analise.Id, ScanId = "scan-1" }, CancellationToken.None);

            analise.ScanId.Should().Be("scan-1");
            analise.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve marcar a análise com erro quando o scanner DAST não devolver scan, liberando a URL")]
        public async Task RegistrarScan_SemScan_DeveMarcarErro()
        {
            var deploy = Construtor.Deploy(1);
            var analise = deploy.IniciarAnaliseDinamica("OWASP_ZAP", 9, "v1.0.0", Url);
            _deployRepo.ObterPorAnaliseAsync(analise.Id, Arg.Any<CancellationToken>()).Returns(deploy);

            await new RegistrarScanAnaliseHandler(_deployRepo).Handle(new RegistrarScanAnaliseCommand { IdAnalise = analise.Id }, CancellationToken.None);

            analise.Status.Should().Be(StatusEnum.ERRO);
        }
    }
}
