using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.ValueObjects;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;
using System.ComponentModel;

namespace Esteira.Domain.Tests.FluxosDeploy.Services.Etapas
{
    public class EtapaAmbienteServicesTests
    {
        private static ValidacaoEtapaValueObject Vo(FluxoDeployEntity fluxo, IEnumerable<AnaliseEntity> analises = null, ConfiguracaoSistemaEntity config = null)
            => new(fluxo, null, analises, config);

        private static IEnumerable<AnaliseEntity> UmaAnalise() => new[] { FluxoTestesSuporte.CriarAnalise() };

        [Fact, DisplayName("Deve validar a conclusão da etapa DEV quando o fluxo e as análises estão válidos")]
        public void Dev_ValidarConclusaoEtapa_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV", "QA");

            new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise())).Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar exceção ao validar a etapa DEV sem análises")]
        public void Dev_SemAnalises_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV");

            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, new List<AnaliseEntity>()));

            act.Should().Throw<DomainException>().WithMessage("Não há deploys válidos para o ambiente de desenvolvimento");
        }

        [Fact, DisplayName("Deve lançar exceção quando a lista de análises for nula")]
        public void Dev_AnalisesNulas_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV");

            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, null));

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o objeto de validação for nulo")]
        public void Dev_ValueObjectNulo_DeveLancarExcecao()
        {
            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando existir análise em execução")]
        public void Dev_AnaliseExecutando_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV");
            var analises = new[] { FluxoTestesSuporte.CriarAnalise("TRIVY", StatusEnum.EXECUTANDO) };

            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, analises));

            act.Should().Throw<DomainException>().WithMessage("As seguintes análises ainda estão em execução: TRIVY");
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo não estiver em execução")]
        public void Dev_FluxoConcluido_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "DEV");
            fluxo.Concluir();

            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise()));

            act.Should().Throw<DomainException>().WithMessage("Fluxo de deploy 5 não está EXECUTANDO!");
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa não estiver em execução")]
        public void Dev_EtapaNaoExecutando_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "QA", "HML");

            var act = () => new EtapaAmbienteDevService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise()));

            act.Should().Throw<DomainException>().WithMessage("A etapa não está em EXECUÇÃO para o fluxo de deploy 5");
        }

        [Fact, DisplayName("Deve validar a conclusão da etapa QA")]
        public void Qa_ValidarConclusaoEtapa_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "QA");

            new EtapaAmbienteQaService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise())).Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar exceção ao validar a etapa QA sem análises")]
        public void Qa_SemAnalises_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "QA");

            var act = () => new EtapaAmbienteQaService().ValidarConclusaoEtapa(Vo(fluxo, new List<AnaliseEntity>()));

            act.Should().Throw<DomainException>().WithMessage("Não há deploys válidos para o ambiente de QA");
        }

        [Fact, DisplayName("Deve validar a conclusão da etapa HML dentro dos limites de vulnerabilidade")]
        public void Hml_DentroDosLimites_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "HML");
            var analises = new[] { FluxoTestesSuporte.CriarAnalise("TRIVY", StatusEnum.CONCLUIDO, "CRITICAL", "HIGH") };
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(5, 5, 5, 5));

            new EtapaAmbienteHmlService().ValidarConclusaoEtapa(Vo(fluxo, analises, config)).Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar exceção na etapa HML quando as vulnerabilidades excederem o limite")]
        public void Hml_ExcedendoLimite_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "HML");
            var analises = new[] { FluxoTestesSuporte.CriarAnalise("TRIVY", StatusEnum.CONCLUIDO, "CRITICAL") };
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(0, 0, 0, 0));

            var act = () => new EtapaAmbienteHmlService().ValidarConclusaoEtapa(Vo(fluxo, analises, config));

            act.Should().Throw<DomainException>().WithMessage("Vulnerabilidades críticas excederam o limite. Atual: 1, Limite: 0");
        }

        [Fact, DisplayName("Deve lançar exceção na etapa HML quando a configuração for nula")]
        public void Hml_ConfiguracaoNula_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "HML");

            var act = () => new EtapaAmbienteHmlService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise()));

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve validar a conclusão da etapa PRD dentro dos limites de vulnerabilidade")]
        public void Prd_DentroDosLimites_DeveRetornarTrue()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "PRD");
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(1, 1, 1, 1));

            new EtapaAmbientePrdService().ValidarConclusaoEtapa(Vo(fluxo, UmaAnalise(), config)).Should().BeTrue();
        }

        [Fact, DisplayName("Deve lançar exceção na etapa PRD quando as vulnerabilidades excederem o limite")]
        public void Prd_ExcedendoLimite_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "PRD");
            var analises = new[] { FluxoTestesSuporte.CriarAnalise("TRIVY", StatusEnum.CONCLUIDO, "HIGH", "HIGH") };
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(0, 1, 0, 0));

            var act = () => new EtapaAmbientePrdService().ValidarConclusaoEtapa(Vo(fluxo, analises, config));

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção na etapa PRD sem análises")]
        public void Prd_SemAnalises_DeveLancarExcecao()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(5, "PRD");
            var config = ConfiguracaoSistemaEntity.Criar("SISTEMA", new LimitesSeveridadeValueObject(1, 1, 1, 1));

            var act = () => new EtapaAmbientePrdService().ValidarConclusaoEtapa(Vo(fluxo, new List<AnaliseEntity>(), config));

            act.Should().Throw<DomainException>().WithMessage("Não há deploys válidos para o ambiente de produção");
        }
    }
}
