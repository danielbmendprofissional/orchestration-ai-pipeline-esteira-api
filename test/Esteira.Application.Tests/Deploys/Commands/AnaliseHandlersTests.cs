using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Commands.AdicionarArtefatosAnalise;
using Esteira.Application.Deploys.Commands.AtualizarAnaliseDast;
using Esteira.Application.DTOs.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Esteira.Application.Tests.Common;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using NSubstitute;

namespace Esteira.Application.Tests.Deploys.Commands
{
    public class AnaliseHandlersTests
    {
        private readonly IDeployRepository _deployRepo = Substitute.For<IDeployRepository>();
        private readonly FakeLogger<AdicionarArtefatosAnaliseHandler> _loggerAdicionar = new();
        private readonly FakeLogger<AtualizarAnaliseDastHandler> _loggerDast = new();

        private void VerificarLogDeErro(int vezes)
            => (_loggerAdicionar.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Error)
                + _loggerDast.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Error))
                .Should().Be(vezes);

        [Fact, DisplayName("Deve adicionar a análise ao deploy com os artefatos e cancelar a anterior da mesma ferramenta")]
        public async Task AdicionarArtefatos_DeveAdicionarAnalise()
        {
            var deploy = Construtor.Deploy(1).ComAnalise("TRIVY");
            _deployRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(deploy);
            var command = new AdicionarArtefatosAnaliseCommand
            {
                DeployId = 1,
                Ferramenta = "TRIVY",
                TipoAnalise = "SAST",
                IdPipeline = 2,
                Tag = "v1.0.1",
                Status = StatusEnum.CONCLUIDO,
                Artefato = new() { new ArtefatoCompletoDto { Severidade = "CRITICAL" }, new ArtefatoCompletoDto { Severidade = "LOW" } }
            };

            await new AdicionarArtefatosAnaliseHandler(_deployRepo, _loggerAdicionar).Handle(command, CancellationToken.None);

            deploy.Analises.Should().HaveCount(2);
            deploy.Analises.First().Status.Should().Be(StatusEnum.CANCELADO);
            var nova = deploy.Analises.Last();
            nova.Artefatos.Should().HaveCount(2);
            nova.Resumo.VulnerabilidadeCritica.Should().Be(1);
            nova.Resumo.VulnerabilidadeBaixa.Should().Be(1);
            VerificarLogDeErro(0);
        }

        [Fact, DisplayName("Deve registrar o erro no log sem propagar quando o deploy não existir")]
        public async Task AdicionarArtefatos_DeployInexistente_DeveLogarErro()
        {
            var act = () => new AdicionarArtefatosAnaliseHandler(_deployRepo, _loggerAdicionar)
                .Handle(new AdicionarArtefatosAnaliseCommand { DeployId = 9, Ferramenta = "TRIVY", TipoAnalise = "SAST" }, CancellationToken.None);

            await act.Should().NotThrowAsync();
            VerificarLogDeErro(1);
        }

        [Theory, DisplayName("Não deve alterar a análise enquanto o scan do scanner DAST estiver em andamento")]
        [InlineData("running")]
        [InlineData("pending")]
        [InlineData("processing")]
        public async Task AtualizarDast_ScanEmAndamento_NaoDeveAlterar(string status)
        {
            await new AtualizarAnaliseDastHandler(_deployRepo, _loggerDast)
                .Handle(new AtualizarAnaliseDastCommand { IdAnalise = 1, StatusScan = status }, CancellationToken.None);

            await _deployRepo.DidNotReceive().ObterPorAnaliseAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Theory, DisplayName("Deve registrar o resultado do scanner DAST com o status correspondente")]
        [InlineData("completed", StatusEnum.CONCLUIDO)]
        [InlineData("canceled", StatusEnum.CANCELADO)]
        [InlineData("aborted", StatusEnum.CANCELADO)]
        [InlineData("stopping", StatusEnum.CANCELADO)]
        public async Task AtualizarDast_ScanFinalizado_DeveRegistrarResultado(string statusScan, StatusEnum esperado)
        {
            var deploy = Construtor.Deploy(1).ComAnalise("OWASP_ZAP", StatusEnum.EXECUTANDO);
            var analise = deploy.Analises.Single().ComId(20);
            _deployRepo.ObterPorAnaliseAsync(20, Arg.Any<CancellationToken>()).Returns(deploy);
            var command = new AtualizarAnaliseDastCommand
            {
                IdAnalise = 20,
                StatusScan = statusScan,
                Artefatos = new() { new ArtefatoCompletoDto { Severidade = "HIGH" } }
            };

            await new AtualizarAnaliseDastHandler(_deployRepo, _loggerDast).Handle(command, CancellationToken.None);

            analise.Status.Should().Be(esperado);
            analise.Resumo.VulnerabilidadeAlta.Should().Be(1);
            analise.Artefatos.Should().ContainSingle().Which.Ferramenta.Should().Be("OWASP_ZAP");
        }

        [Theory, DisplayName("Deve marcar a análise com erro quando o scan do scanner DAST falhar")]
        [InlineData("failed")]
        [InlineData("error")]
        public async Task AtualizarDast_ScanComFalha_DeveMarcarErro(string statusScan)
        {
            var deploy = Construtor.Deploy(1).ComAnalise("OWASP_ZAP", StatusEnum.EXECUTANDO);
            var analise = deploy.Analises.Single().ComId(20);
            _deployRepo.ObterPorAnaliseAsync(20, Arg.Any<CancellationToken>()).Returns(deploy);

            await new AtualizarAnaliseDastHandler(_deployRepo, _loggerDast)
                .Handle(new AtualizarAnaliseDastCommand { IdAnalise = 20, StatusScan = statusScan, Artefatos = new() }, CancellationToken.None);

            analise.Status.Should().Be(StatusEnum.ERRO);
        }

        [Fact, DisplayName("Deve registrar o erro no log quando a análise do scanner DAST não existir")]
        public async Task AtualizarDast_AnaliseInexistente_DeveLogarErro()
        {
            _deployRepo.ObterPorAnaliseAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((DeployEntity)null);

            await new AtualizarAnaliseDastHandler(_deployRepo, _loggerDast)
                .Handle(new AtualizarAnaliseDastCommand { IdAnalise = 9, StatusScan = "completed" }, CancellationToken.None);

            VerificarLogDeErro(1);
        }
    }
}
