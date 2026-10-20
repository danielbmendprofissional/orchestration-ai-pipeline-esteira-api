using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.Deploys.Entities;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
namespace Esteira.Domain.Tests.Deploys.Entities
{
    public class AnaliseEntityTests
    {
        private static (DeployEntity Deploy, AnaliseEntity Analise) CriarDeployComAnaliseDast()
        {
            var deploy = FluxoTestesSuporte.CriarDeploy();
            var analise = deploy.AdicionarAnalise("OWASP_ZAP", "DAST", 1, "v1.0.0", "scan", StatusEnum.EXECUTANDO);
            analise.Id = 10;
            return (deploy, analise);
        }

        [Fact, DisplayName("Deve registrar o resultado da análise substituindo artefatos, resumo e status")]
        public void RegistrarResultadoAnalise_DeveAtualizarResumoStatusEArtefatos()
        {
            var (deploy, analise) = CriarDeployComAnaliseDast();

            deploy.RegistrarResultadoAnalise(10, StatusEnum.CONCLUIDO, FluxoTestesSuporte.Artefatos("MEDIUM", "WARNING"));

            analise.Status.Should().Be(StatusEnum.CONCLUIDO);
            analise.Resumo.Should().Be(new ResumoSeveridadeValueObject(0, 0, 2, 0));
            analise.Artefatos.Should().HaveCount(2)
                .And.OnlyContain(a => a.Ferramenta == "OWASP_ZAP");
        }

        [Fact, DisplayName("Deve substituir os artefatos anteriores ao registrar um novo resultado")]
        public void RegistrarResultadoAnalise_DeveSubstituirArtefatosAnteriores()
        {
            var (deploy, analise) = CriarDeployComAnaliseDast();
            deploy.RegistrarResultadoAnalise(10, StatusEnum.CONCLUIDO, FluxoTestesSuporte.Artefatos("HIGH", "HIGH", "HIGH"));

            deploy.RegistrarResultadoAnalise(10, StatusEnum.CANCELADO, FluxoTestesSuporte.Artefatos());

            analise.Artefatos.Should().BeEmpty();
            analise.Resumo.Should().Be(ResumoSeveridadeValueObject.Vazio);
            analise.Status.Should().Be(StatusEnum.CANCELADO);
        }

        [Fact, DisplayName("Deve lançar NotFoundException quando a análise não pertencer ao deploy")]
        public void RegistrarResultadoAnalise_AnaliseInexistente_DeveLancarNotFoundException()
        {
            var (deploy, _) = CriarDeployComAnaliseDast();

            var act = () => deploy.RegistrarResultadoAnalise(99, StatusEnum.CONCLUIDO, FluxoTestesSuporte.Artefatos());

            act.Should().Throw<NotFoundException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao registrar resultado com artefatos nulos")]
        public void RegistrarResultadoAnalise_ArtefatosNulos_DeveLancarDomainException()
        {
            var (deploy, _) = CriarDeployComAnaliseDast();

            var act = () => deploy.RegistrarResultadoAnalise(10, StatusEnum.CONCLUIDO, null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve atualizar o status da análise")]
        public void AtualizarStatus_DeveAtualizarStatus()
        {
            var (_, analise) = CriarDeployComAnaliseDast();

            analise.AtualizarStatus(StatusEnum.CANCELADO);

            analise.Status.Should().Be(StatusEnum.CANCELADO);
        }

        [Fact, DisplayName("Deve considerar somente as análises concluídas, em execução e com erro")]
        public void StatusConsiderados_DeveConterApenasStatusEsperados()
        {
            AnaliseEntity.StatusConsiderados.Should().BeEquivalentTo(new[] { StatusEnum.CONCLUIDO, StatusEnum.EXECUTANDO, StatusEnum.ERRO });
        }
    }
}
