using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Services;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
using System.ComponentModel;

namespace Esteira.Domain.Tests.FluxosDeploy.Services
{
    public class FluxoDeployServiceTests
    {
        private readonly FluxoDeployService _service = new();

        private static DeployEntity Deploy(int id, int idFluxoDeploy, int idProjeto, string tag)
            => FluxoTestesSuporte.CriarDeploy(id, idFluxoDeploy, idProjeto, tag);

        [Fact, DisplayName("Deve lançar exceção ao validar deploy sem fluxo")]
        public void DeployValido_FluxoNulo_DeveLancarExcecao()
        {
            var act = () => _service.DeployValido(null, "DEV");

            act.Should().Throw<DomainException>().WithMessage("É preciso ter um fluxo de deploy criado*");
        }

        [Fact, DisplayName("Deve lançar exceção ao validar deploy com fluxo que não está em execução")]
        public void DeployValido_FluxoConcluido_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV");
            fluxo.Concluir();

            var act = () => _service.DeployValido(fluxo, "DEV");

            act.Should().Throw<DomainException>().WithMessage("O fluxo de deploy do sistema SISTEMA não está válido!");
        }

        [Theory, DisplayName("Deve lançar exceção quando a etapa for nula ou vazia")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void DeployValido_EtapaVazia_DeveLancarExcecao(string etapa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV");

            var act = () => _service.DeployValido(fluxo, etapa);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa não permitir deploy")]
        public void DeployValido_EtapaNaoPermiteDeploy_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV");

            var act = () => _service.DeployValido(fluxo, "XYZ");

            act.Should().Throw<DomainException>().WithMessage("Não é possível fazer deploy na etapa XYZ");
        }

        [Theory, DisplayName("Deve aceitar deploy em ambiente técnico quando DEV ou QA estiver em execução")]
        [InlineData("DEV")]
        [InlineData("dev")]
        [InlineData("QA")]
        public void DeployValido_AmbienteTecnicoExecutando_NaoDeveLancarExcecao(string etapa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV", "QA");

            var act = () => _service.DeployValido(fluxo, etapa);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção em ambiente técnico quando nenhuma etapa técnica estiver em execução")]
        public void DeployValido_AmbienteTecnicoSemExecucao_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");

            var act = () => _service.DeployValido(fluxo, "DEV");

            act.Should().Throw<DomainException>().WithMessage("Não é possível executar a ação pois nenhuma das etapas a seguir está em EXECUÇÃO*");
        }

        [Fact, DisplayName("Deve aceitar deploy em HML quando a etapa estiver em execução")]
        public void DeployValido_HmlExecutando_NaoDeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");

            var act = () => _service.DeployValido(fluxo, "HML");

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção em PRD quando a etapa não estiver em execução")]
        public void DeployValido_PrdNaoExecutando_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");

            var act = () => _service.DeployValido(fluxo, "PRD");

            act.Should().Throw<DomainException>().WithMessage("Não é possível executar pois a etapa PRD não está em EXECUÇÃO ");
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa técnica informada já estiver concluída")]
        public void DeployValido_EtapaConcluida_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV", "QA");
            fluxo.Etapas.Single(e => e.SglEtapa == "DEV").Concluir();
            fluxo.Etapas.Single(e => e.SglEtapa == "QA").Iniciar();

            var act = () => _service.DeployValido(fluxo, "DEV");

            act.Should().Throw<DomainException>().WithMessage("Não é possível executar pois a etapa DEV está CONCLUÍDA ");
        }

        [Fact, DisplayName("Não deve lançar exceção ao validar novo fluxo quando não houver fluxo atual")]
        public void ValidarNovoFluxo_FluxoNulo_NaoDeveLancarExcecao()
        {
            var act = () => _service.ValidarNovoFluxo(null);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção ao criar novo fluxo quando a homologação do atual não foi iniciada")]
        public void ValidarNovoFluxo_HmlNaoIniciada_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV", "HML");

            var act = () => _service.ValidarNovoFluxo(fluxo);

            act.Should().Throw<DomainException>().WithMessage("Não é possível criar um novo fluxo para a aplicação SISTEMA sem que a atual esteja pelo menos em homologação");
        }

        [Fact, DisplayName("Não deve lançar exceção ao criar novo fluxo quando o atual estiver em homologação")]
        public void ValidarNovoFluxo_HmlExecutando_NaoDeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "HML");

            var act = () => _service.ValidarNovoFluxo(fluxo);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Não deve validar a seleção de etapa quando não houver outro fluxo aberto")]
        public void ValidarSelecaoEtapa_SemFluxoAberto_NaoDeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "HML");

            var act = () => _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(fluxo, "DEV");

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção ao selecionar etapa fora de HML, AAN e PRD com fluxo atual em ambiente final")]
        public void ValidarSelecaoEtapa_EtapaForaDosAmbientes_DeveLancarExcecao()
        {
            var atual = FluxoTestesSuporte.CriarFluxo(1, "HML");
            var aberto = FluxoTestesSuporte.CriarFluxo(2, "DEV");

            var act = () => _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(atual, "DEV", aberto);

            act.Should().Throw<DomainException>().WithMessage("A etapa não pode ser selecionada pois existe outro fluxo de deploy em execução*");
        }

        [Theory, DisplayName("Deve aceitar a seleção de etapa dos ambientes finais ignorando maiúsculas e minúsculas")]
        [InlineData("HML")]
        [InlineData("hml")]
        [InlineData("aan")]
        [InlineData("Prd")]
        public void ValidarSelecaoEtapa_EtapaDosAmbientes_NaoDeveLancarExcecao(string etapa)
        {
            var atual = FluxoTestesSuporte.CriarFluxo(1, "HML");
            var aberto = FluxoTestesSuporte.CriarFluxo(2, "DEV");

            var act = () => _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(atual, etapa, aberto);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve aceitar a seleção de qualquer etapa quando o fluxo atual está em ambiente técnico")]
        public void ValidarSelecaoEtapa_FluxoAtualEmDev_NaoDeveLancarExcecao()
        {
            var atual = FluxoTestesSuporte.CriarFluxo(1, "DEV", "HML");
            var aberto = FluxoTestesSuporte.CriarFluxo(2, "HML");

            var act = () => _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(atual, "DEV", aberto);

            act.Should().NotThrow();
        }

        [Fact, DisplayName("Deve lançar exceção de domínio quando o fluxo atual for nulo e houver fluxo aberto")]
        public void ValidarSelecaoEtapa_FluxoAtualNulo_DeveLancarExcecao()
        {
            var aberto = FluxoTestesSuporte.CriarFluxo(2, "DEV");

            var act = () => _service.ValidarSelecaoEtapaEspecificaQuandoHaMaisDeUmFluxo(null, "DEV", aberto);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve retornar nulo quando não houver fluxos")]
        public void ObterFluxoAtual_SemFluxos_DeveRetornarNulo()
        {
            _service.ObterFluxoAtual(new List<FluxoDeployEntity>(), new List<DeployEntity>(), "feature-dev", "v1.0.0", 10).Should().BeNull();
        }

        [Fact, DisplayName("Deve retornar o único fluxo quando houver apenas um")]
        public void ObterFluxoAtual_UmFluxo_DeveRetornarOFluxo()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV");

            _service.ObterFluxoAtual(new[] { fluxo }, new List<DeployEntity>(), "master", "v1.0.0", 10).Should().BeSameAs(fluxo);
        }

        [Fact, DisplayName("Deve lançar exceção de domínio quando os parâmetros forem nulos ou vazios")]
        public void ObterFluxoAtual_ParametrosInvalidos_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV");
            var deploys = new List<DeployEntity>();

            var act1 = () => _service.ObterFluxoAtual(null, deploys, "dev", "v1.0.0", 10);
            var act2 = () => _service.ObterFluxoAtual(new[] { fluxo }, null, "dev", "v1.0.0", 10);
            var act3 = () => _service.ObterFluxoAtual(new[] { fluxo }, deploys, null, "v1.0.0", 10);
            var act4 = () => _service.ObterFluxoAtual(new[] { fluxo }, deploys, "dev", " ", 10);

            act1.Should().Throw<DomainException>();
            act2.Should().Throw<DomainException>();
            act3.Should().Throw<DomainException>();
            act4.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve retornar nulo quando nenhum fluxo corresponder à branch")]
        public void ObterFluxoAtual_BranchSemCorrespondencia_DeveRetornarNulo()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");

            _service.ObterFluxoAtual(new[] { a, b }, new List<DeployEntity>(), "master", "v1.0.0", 10).Should().BeNull();
        }

        [Fact, DisplayName("Deve retornar o fluxo da branch quando o outro fluxo não tiver versão cadastrada para o projeto")]
        public void ObterFluxoAtual_OutroFluxoSemVersao_DeveRetornarFluxo()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");

            _service.ObterFluxoAtual(new[] { a, b }, new List<DeployEntity>(), "feature-dev", "v2.0.0", 10).Should().BeSameAs(b);
        }

        [Fact, DisplayName("Deve retornar o fluxo da branch quando os deploys forem somente de outros projetos")]
        public void ObterFluxoAtual_DeploysApenasDeOutroProjeto_DeveRetornarFluxo()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");
            var deploys = new[] { Deploy(1, idFluxoDeploy: 2, idProjeto: 99, "v2.0.0") };

            _service.ObterFluxoAtual(new[] { a, b }, deploys, "feature-dev", "v2.0.0", 10).Should().BeSameAs(b);
        }

        [Fact, DisplayName("Deve lançar exceção quando a versão da tag for menor que a do outro fluxo e o projeto não tiver deploy")]
        public void ObterFluxoAtual_TagMenorQueOutroFluxo_DeveLancarExcecao()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");
            var deploys = new[] { Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v2.0.0") };

            var act = () => _service.ObterFluxoAtual(new[] { a, b }, deploys, "feature-dev", "v1.0.0", 10);

            act.Should().Throw<DomainException>().WithMessage("A versão da tag tem que ser maior que v2");
        }

        [Fact, DisplayName("Deve retornar o fluxo da branch quando a tag for maior que a do outro fluxo")]
        public void ObterFluxoAtual_TagMaiorQueOutroFluxo_DeveRetornarFluxo()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");
            var deploys = new[] { Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v2.0.0") };

            _service.ObterFluxoAtual(new[] { a, b }, deploys, "feature-dev", "v3.0.0", 10).Should().BeSameAs(b);
        }

        [Fact, DisplayName("Deve lançar exceção quando a tag não iniciar com a versão dos deploys do projeto no fluxo")]
        public void ObterFluxoAtual_TagDiferenteDosDeploysDoFluxo_DeveLancarExcecao()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");
            var deploys = new[]
            {
                Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v2.0.0"),
                Deploy(2, idFluxoDeploy: 2, idProjeto: 10, "v3.0.0")
            };

            var act = () => _service.ObterFluxoAtual(new[] { a, b }, deploys, "feature-dev", "v4.0.0", 10);

            act.Should().Throw<DomainException>().WithMessage("Versão aplicação do fluxo de deploy está incorreta*v3*Tag passada: v4");
        }

        [Fact, DisplayName("Deve retornar o fluxo quando a tag iniciar com a versão dos deploys do projeto no fluxo")]
        public void ObterFluxoAtual_TagIgualAosDeploysDoFluxo_DeveRetornarFluxo()
        {
            var a = FluxoTestesSuporte.CriarFluxo(1, "HML", "PRD");
            var b = FluxoTestesSuporte.CriarFluxo(2, "DEV", "QA");
            var deploys = new[]
            {
                Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v2.0.0"),
                Deploy(2, idFluxoDeploy: 2, idProjeto: 10, "v3.0.0")
            };

            _service.ObterFluxoAtual(new[] { a, b }, deploys, "feature-dev", "v3.1.0", 10).Should().BeSameAs(b);
        }

        private static (FluxoDeployEntity F1, FluxoDeployEntity F2) FluxoEmHmlEFluxoEmDev()
        {
            var f1 = FluxoTestesSuporte.CriarFluxo(1, "DEV", "HML", "PRD");
            f1.AvancarEtapa();
            var f2 = FluxoTestesSuporte.CriarFluxo(2, "DEV", "HML", "PRD");
            return (f1, f2);
        }

        private static DeployEntity[] DeploysDotnetEAngular()
            => new[]
            {
                Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v1.0.0"),
                Deploy(2, idFluxoDeploy: 1, idProjeto: 20, "v40.0.0"),
                Deploy(3, idFluxoDeploy: 2, idProjeto: 10, "v2.0.0"),
                Deploy(4, idFluxoDeploy: 2, idProjeto: 20, "v41.0.0")
            };

        [Theory, DisplayName("Deve enviar o deploy de HML para o fluxo que está executando HML independente da ordem dos fluxos")]
        [InlineData(false)]
        [InlineData(true)]
        public void ObterFluxoAtual_BranchHml_DeveEscolherFluxoExecutandoHml(bool inverterOrdem)
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();
            var fluxos = inverterOrdem ? new[] { f2, f1 } : new[] { f1, f2 };

            _service.ObterFluxoAtual(fluxos, DeploysDotnetEAngular(), "hml", "v40.2.0", 20).Should().BeSameAs(f1);
            _service.ObterFluxoAtual(fluxos, DeploysDotnetEAngular(), "hml", "v1.3.0", 10).Should().BeSameAs(f1);
        }

        [Fact, DisplayName("Deve enviar o deploy de DEV para o fluxo que ainda está em DEV")]
        public void ObterFluxoAtual_BranchDev_DeveEscolherFluxoEmDev()
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();

            _service.ObterFluxoAtual(new[] { f1, f2 }, DeploysDotnetEAngular(), "dev", "v41.1.0", 20).Should().BeSameAs(f2);
            _service.ObterFluxoAtual(new[] { f1, f2 }, DeploysDotnetEAngular(), "dev", "v2.1.0", 10).Should().BeSameAs(f2);
        }

        [Fact, DisplayName("Deve rejeitar deploy em HML com a versão do fluxo mais novo quando o projeto já tem deploy no fluxo em HML")]
        public void ObterFluxoAtual_BranchHmlComVersaoDoFluxoNovo_DeveLancarExcecao()
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();

            var act = () => _service.ObterFluxoAtual(new[] { f2, f1 }, DeploysDotnetEAngular(), "hml", "v41.0.0", 20);

            act.Should().Throw<DomainException>().WithMessage("Versão aplicação do fluxo de deploy está incorreta*v40*");
        }

        [Fact, DisplayName("Deve rejeitar deploy em HML com a versão do fluxo mais novo quando o projeto ainda não tem deploy no fluxo em HML")]
        public void ObterFluxoAtual_ProjetoNovoNoFluxoEmHmlComVersaoDoFluxoNovo_DeveLancarExcecao()
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();
            var deploys = new[] { Deploy(1, idFluxoDeploy: 2, idProjeto: 30, "v5.0.0") };

            var act = () => _service.ObterFluxoAtual(new[] { f1, f2 }, deploys, "hml", "v5.0.0", 30);

            act.Should().Throw<DomainException>().WithMessage("A versão da tag tem que ser menor que v5");
        }

        [Fact, DisplayName("Deve aceitar deploy em HML de projeto novo no fluxo mais antigo com versão menor que a do fluxo mais novo")]
        public void ObterFluxoAtual_ProjetoNovoNoFluxoEmHmlComVersaoMenor_DeveRetornarFluxoAntigo()
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();
            var deploys = new[] { Deploy(1, idFluxoDeploy: 2, idProjeto: 30, "v5.0.0") };

            _service.ObterFluxoAtual(new[] { f1, f2 }, deploys, "hml", "v4.0.0", 30).Should().BeSameAs(f1);
        }

        [Fact, DisplayName("Deve aceitar deploy em HML de projeto que o fluxo mais novo ainda não tocou")]
        public void ObterFluxoAtual_ProjetoSemDeployNoOutroFluxo_DeveRetornarFluxo()
        {
            var (f1, f2) = FluxoEmHmlEFluxoEmDev();
            var deploys = new[] { Deploy(1, idFluxoDeploy: 1, idProjeto: 10, "v1.0.0") };

            _service.ObterFluxoAtual(new[] { f1, f2 }, deploys, "hml", "v1.2.0", 10).Should().BeSameAs(f1);
        }

        [Fact, DisplayName("Não deve validar a entrada na etapa quando não houver outro fluxo ou etapa")]
        public void ValidarEntradaEtapa_SemOutroFluxoOuEtapa_NaoDeveLancar()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(2, "HML");
            var outro = FluxoTestesSuporte.CriarFluxo(1, "HML");

            var semOutro = () => _service.ValidarEntradaEtapa(fluxo, "HML");
            var semEtapa = () => _service.ValidarEntradaEtapa(fluxo, null, outro);

            semOutro.Should().NotThrow();
            semEtapa.Should().NotThrow();
        }

        [Theory, DisplayName("Deve impedir entrar numa etapa que o outro fluxo está executando ignorando maiúsculas e minúsculas")]
        [InlineData("ABER")]
        [InlineData("dev")]
        [InlineData("HML")]
        [InlineData("aan")]
        public void ValidarEntradaEtapa_EtapaCompartilhada_DeveLancarExcecao(string etapa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(2, "ABER");
            var outro = FluxoTestesSuporte.CriarFluxo(1, etapa.ToUpper());

            var act = () => _service.ValidarEntradaEtapa(fluxo, etapa, outro);

            act.Should().Throw<DomainException>().WithMessage($"A etapa {etapa} já está em execução no fluxo de deploy 1");
        }

        [Fact, DisplayName("Deve permitir o fluxo mais novo entrar em HML quando o outro estiver em PRD")]
        public void ValidarEntradaEtapa_OutroEmPrd_DevePermitirHml()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(2, "QA", "HML");
            var outro = FluxoTestesSuporte.CriarFluxo(1, "PRD");

            var act = () => _service.ValidarEntradaEtapa(fluxo, "HML", outro);

            act.Should().NotThrow();
        }

        [Theory, DisplayName("Deve impedir o fluxo mais novo de entrar em PRD enquanto o mais antigo estiver em execução")]
        [InlineData(2)]
        [InlineData(0)]
        public void ValidarEntradaEtapa_MaisNovoEntrandoEmPrd_DeveLancarExcecao(int idFluxo)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(idFluxo, "HML", "PRD");
            var outro = FluxoTestesSuporte.CriarFluxo(1, "AAN");

            var act = () => _service.ValidarEntradaEtapa(fluxo, "PRD", outro);

            act.Should().Throw<DomainException>().WithMessage("Não é possível iniciar a etapa de PRODUÇÃO antes da conclusão do fluxo de deploy 1");
        }

        [Fact, DisplayName("Deve permitir o fluxo mais antigo entrar em PRD com o mais novo em outra etapa")]
        public void ValidarEntradaEtapa_MaisAntigoEntrandoEmPrd_DevePermitir()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AAN", "PRD");
            var outro = FluxoTestesSuporte.CriarFluxo(2, "HML");

            var act = () => _service.ValidarEntradaEtapa(fluxo, "PRD", outro);

            act.Should().NotThrow();
        }
    }
}
