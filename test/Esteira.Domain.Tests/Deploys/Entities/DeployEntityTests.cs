using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Entities;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.Deploys.Entities
{
    public class DeployEntityTests
    {
        private static DeployEntity CriarDeploy(string branch = "BRANCH", string tag = "v2.3.1", StatusEnum status = StatusEnum.EXECUTANDO)
            => DeployEntity.Criar(1, 10, "projeto", status, branch, "java", tag);

        #region Criar

        [Fact, DisplayName("Deve extrair a versão da aplicação da tag")]
        public void Criar_DeveExtrairVersaoAplicacaoDaTag()
        {
            CriarDeploy(tag: "v2.3.1").VersaoAplicacao.Should().Be("v2");
        }

        [Fact, DisplayName("Deve aceitar branch com exatamente 20 caracteres")]
        public void Criar_BranchComLimite_DeveCriar()
        {
            var act = () => CriarDeploy(branch: new string('A', 20));

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção quando a branch ultrapassar 20 caracteres")]
        public void Criar_BranchMaiorQueLimite_DeveLancarDomainException()
        {
            var act = () => CriarDeploy(branch: new string('A', 21));

            act.Should().Throw<DomainException>();
        }

        [Theory, DisplayName("Deve lançar exceção quando a branch for vazia")]
        [InlineData(null)]
        [InlineData("")]
        public void Criar_BranchVazia_DeveLancarDomainException(string branch)
        {
            var act = () => CriarDeploy(branch: branch);

            act.Should().Throw<DomainException>();
        }

        [Theory, DisplayName("Deve lançar exceção quando a tag for inválida")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void Criar_TagInvalida_DeveLancarDomainException(string tag)
        {
            var act = () => CriarDeploy(tag: tag);

            act.Should().Throw<DomainException>();
        }

        [Theory, DisplayName("Deve lançar exceção quando o fluxo de deploy for inválido")]
        [InlineData(0)]
        [InlineData(-1)]
        public void Criar_FluxoInvalido_DeveLancarDomainException(int idFluxoDeploy)
        {
            var act = () => DeployEntity.Criar(idFluxoDeploy, 10, "projeto", StatusEnum.EXECUTANDO, "BRANCH", "java", "v1.0.0");

            act.Should().Throw<DomainException>();
        }

        #endregion

        #region ValidarVersaoAplicacao

        [Fact, DisplayName("Deve aceitar tag da mesma versão com o deploy em execução")]
        public void ValidarVersaoAplicacao_MesmaVersao_NaoDeveLancarExcecao()
        {
            var act = () => CriarDeploy(tag: "v2.0.0").ValidarVersaoAplicacao("v2.5.0");

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção para tag de outra versão com o deploy em execução")]
        public void ValidarVersaoAplicacao_OutraVersao_DeveLancarDomainException()
        {
            var act = () => CriarDeploy(tag: "v2.0.0").ValidarVersaoAplicacao("v3.0.0");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Não deve lançar exceção para tag de outra versão quando o deploy não estiver em execução")]
        public void ValidarVersaoAplicacao_DeployConcluido_NaoDeveLancarExcecao()
        {
            var act = () => CriarDeploy(tag: "v2.0.0", status: StatusEnum.CONCLUIDO).ValidarVersaoAplicacao("v3.0.0");

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Não deve confundir versão 2 com versão 20")]
        public void ValidarVersaoAplicacao_Versao2Com20_DeveLancarDomainException()
        {
            var act = () => CriarDeploy(tag: "v2.0.0").ValidarVersaoAplicacao("v20.0.0");

            act.Should().Throw<DomainException>();
        }

        #endregion

        #region Status

        [Fact, DisplayName("Deve atualizar o status do deploy")]
        public void AtualizarStatus_DeveAtualizar()
        {
            var deploy = CriarDeploy();

            deploy.AtualizarStatus(StatusEnum.CANCELADO);

            deploy.Status.Should().Be(StatusEnum.CANCELADO);
        }

        [Fact, DisplayName("Deve concluir o deploy em execução")]
        public void Concluir_DeployExecutando_DeveConcluir()
        {
            var deploy = CriarDeploy();

            deploy.Concluir();

            deploy.Status.Should().Be(StatusEnum.CONCLUIDO);
        }

        [Fact, DisplayName("Deve lançar exceção ao concluir deploy que não está em execução")]
        public void Concluir_DeployCancelado_DeveLancarDomainException()
        {
            var deploy = CriarDeploy(status: StatusEnum.CANCELADO);

            var act = () => deploy.Concluir();

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve cancelar o deploy e todas as suas análises")]
        public void Cancelar_DeveCancelarDeployEAnalises()
        {
            var deploy = CriarDeploy();
            deploy.AdicionarAnalise("TRIVY", "SCA", 1, "v2.3.1", null, StatusEnum.CONCLUIDO);
            deploy.AdicionarAnalise("OWASP_ZAP", "DAST", 1, "v2.3.1", "scan", StatusEnum.EXECUTANDO);

            deploy.Cancelar();

            deploy.Status.Should().Be(StatusEnum.CANCELADO);
            deploy.Analises.Should().OnlyContain(a => a.Status == StatusEnum.CANCELADO);
        }

        #endregion

        #region AdicionarAnalise

        [Fact, DisplayName("Deve adicionar a análise calculando o resumo pelas severidades dos artefatos")]
        public void AdicionarAnalise_DeveCalcularResumo()
        {
            var deploy = CriarDeploy();

            var analise = deploy.AdicionarAnalise("TRIVY", "SCA", 5, "v2.3.1", null, StatusEnum.CONCLUIDO,
                FluxoTestesSuporte.Artefatos("CRITICAL", "HIGH", "ERROR", "LOW"));

            deploy.Analises.Should().ContainSingle().Which.Should().BeSameAs(analise);
            analise.Ferramenta.Should().Be("TRIVY");
            analise.TipoAnalise.Should().Be("SCA");
            analise.IdPipeline.Should().Be(5);
            analise.Status.Should().Be(StatusEnum.CONCLUIDO);
            analise.Artefatos.Should().HaveCount(4);
            analise.Resumo.VulnerabilidadeCritica.Should().Be(1);
            analise.Resumo.VulnerabilidadeAlta.Should().Be(2);
            analise.Resumo.VulnerabilidadeBaixa.Should().Be(1);
        }

        [Fact, DisplayName("Deve criar análise com resumo vazio quando não houver artefatos")]
        public void AdicionarAnalise_SemArtefatos_ResumoVazio()
        {
            var analise = CriarDeploy().AdicionarAnalise("OWASP_ZAP", "DAST", 1, "v2.3.1", "scan", StatusEnum.EXECUTANDO);

            analise.Artefatos.Should().BeEmpty();
            analise.Resumo.Total.Should().Be(0);
            analise.ScanId.Should().Be("scan");
        }

        [Fact, DisplayName("Deve cancelar as análises anteriores da mesma ferramenta ignorando maiúsculas e minúsculas")]
        public void AdicionarAnalise_MesmaFerramenta_DeveCancelarAnteriores()
        {
            var deploy = CriarDeploy();
            var anterior = deploy.AdicionarAnalise("TRIVY", "SCA", 1, "v2.3.1", null, StatusEnum.CONCLUIDO);
            var outraFerramenta = deploy.AdicionarAnalise("SEMGREP", "SAST", 1, "v2.3.1", null, StatusEnum.CONCLUIDO);

            var nova = deploy.AdicionarAnalise("trivy", "SCA", 2, "v2.3.2", null, StatusEnum.CONCLUIDO);

            anterior.Status.Should().Be(StatusEnum.CANCELADO);
            outraFerramenta.Status.Should().Be(StatusEnum.CONCLUIDO);
            nova.Status.Should().Be(StatusEnum.CONCLUIDO);
            deploy.Analises.Should().HaveCount(3);
        }

        [Theory, DisplayName("Deve lançar exceção quando ferramenta ou tipo de análise forem vazios")]
        [InlineData(null, "SCA")]
        [InlineData("", "SCA")]
        [InlineData("TRIVY", null)]
        [InlineData("TRIVY", "")]
        public void AdicionarAnalise_DadosVazios_DeveLancarDomainException(string ferramenta, string tipoAnalise)
        {
            var deploy = CriarDeploy();

            var act = () => deploy.AdicionarAnalise(ferramenta, tipoAnalise, 1, "v2.3.1", null, StatusEnum.CONCLUIDO);

            act.Should().Throw<DomainException>();
            deploy.Analises.Should().BeEmpty();
        }

        [Fact, DisplayName("Não deve cancelar análises anteriores quando a nova análise for inválida")]
        public void AdicionarAnalise_Invalida_NaoDeveCancelarAnteriores()
        {
            var deploy = CriarDeploy();
            var anterior = deploy.AdicionarAnalise("TRIVY", "SCA", 1, "v2.3.1", null, StatusEnum.CONCLUIDO);

            var act = () => deploy.AdicionarAnalise("TRIVY", null, 1, "v2.3.1", null, StatusEnum.CONCLUIDO);

            act.Should().Throw<DomainException>();
            anterior.Status.Should().Be(StatusEnum.CONCLUIDO);
        }

        #endregion

        #region Análise dinâmica

        [Fact, DisplayName("Deve iniciar a análise dinâmica em execução, com a URL e sem scan")]
        public void IniciarAnaliseDinamica_DeveCriarAnaliseEmExecucao()
        {
            var deploy = CriarDeploy();

            var analise = deploy.IniciarAnaliseDinamica("OWASP_ZAP", 9, "v2.3.1", "https://app.exemplo.com");

            analise.TipoAnalise.Should().Be("DAST");
            analise.Status.Should().Be(StatusEnum.EXECUTANDO);
            analise.UrlAlvo.Should().Be("https://app.exemplo.com");
            analise.ScanId.Should().BeNull();
        }

        [Theory, DisplayName("Deve exigir a URL ao iniciar a análise dinâmica")]
        [InlineData(null)]
        [InlineData("")]
        public void IniciarAnaliseDinamica_SemUrl_DeveLancarDomainException(string url)
        {
            var act = () => CriarDeploy().IniciarAnaliseDinamica("OWASP_ZAP", 9, "v2.3.1", url);

            act.Should().Throw<DomainException>().WithMessage("URL da análise é obrigatória");
        }

        [Fact, DisplayName("Deve registrar o scan na análise em execução")]
        public void AtribuirScanAnalise_DeveRegistrarScan()
        {
            var deploy = CriarDeploy();
            var analise = deploy.IniciarAnaliseDinamica("OWASP_ZAP", 9, "v2.3.1", "https://app");

            deploy.AtribuirScanAnalise(analise.Id, "scan-1");

            analise.ScanId.Should().Be("scan-1");
            analise.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve impedir registrar scan em análise que não está em execução")]
        public void AtribuirScanAnalise_AnaliseComErro_DeveLancarDomainException()
        {
            var deploy = CriarDeploy();
            var analise = deploy.IniciarAnaliseDinamica("OWASP_ZAP", 9, "v2.3.1", "https://app");
            deploy.FalharAnalise(analise.Id);

            var act = () => deploy.AtribuirScanAnalise(analise.Id, "scan-1");

            act.Should().Throw<DomainException>();
            analise.Status.Should().Be(StatusEnum.ERRO);
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao registrar scan em análise inexistente")]
        public void AtribuirScanAnalise_AnaliseInexistente_DeveLancarNotFound()
        {
            var act = () => CriarDeploy().AtribuirScanAnalise(99, "scan-1");

            act.Should().Throw<NotFoundException>();
        }

        #endregion
    }
}
