using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Application.Tests.Common;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Commands.Rules
{
    public class EtapaRulesTests
    {
        private static readonly IEtapaRulesService[] Servicos =
        {
            new EtapaAberturaService(), new EtapaVersaoTarefasService(), new EtapaGmudService(),
            new EtapaAmbienteDevService(), new EtapaAmbienteHmlService(), new EtapaAmbienteQaService(),
            new EtapaAmbientePrdService(), new EtapaAreaNegocioService(), new EtapaEquipeTecnicaService(),
            new EtapaBancoDeDadosService()
        };

        private readonly IDeployRepository _deployRepo = Substitute.For<IDeployRepository>();
        private readonly IConfiguracaoSistemaRepository _configRepo = Substitute.For<IConfiguracaoSistemaRepository>();
        private readonly IAprovadorTecnicoRepository _aprovadorRepo = Substitute.For<IAprovadorTecnicoRepository>();
        private readonly IGerenciadorTarefas _gerenciadorTarefas = Substitute.For<IGerenciadorTarefas>();

        public EtapaRulesTests()
        {
            _configRepo.GetBySistemaAsync("SISTEMA", Arg.Any<CancellationToken>()).Returns(Construtor.Configuracao());
        }

        private void Deploys(params DeployEntity[] deploys)
            => _deployRepo.ObterPorFluxosAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(deploys.ToList());

        [Fact, DisplayName("Deve validar a etapa DEV e concluir somente os deploys em execução da branch DEV")]
        public async Task Dev_DeveConcluirDeploysDaBranch()
        {
            var fluxo = Construtor.Fluxo(1, "DEV", "QA");
            var deployDev = Construtor.Deploy(1, branch: "DEV").ComAnalise();
            var deployQa = Construtor.Deploy(2, branch: "QA").ComAnalise();
            Deploys(deployDev, deployQa);

            await new AmbienteDevRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(fluxo);

            deployDev.Status.Should().Be(StatusEnum.CONCLUIDO);
            deployQa.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve lançar exceção na etapa DEV sem deploys válidos")]
        public async Task Dev_SemDeploys_DeveLancarExcecao()
        {
            Deploys();

            var act = () => new AmbienteDevRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "DEV"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não há deploys válidos para o ambiente de desenvolvimento");
        }

        [Fact, DisplayName("Deve desconsiderar análises canceladas na validação de ambiente")]
        public async Task Dev_AnaliseCancelada_DeveSerDesconsiderada()
        {
            Deploys(Construtor.Deploy(1, branch: "DEV").ComAnalise(status: StatusEnum.CANCELADO));

            var act = () => new AmbienteDevRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "DEV"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não há deploys válidos*");
        }

        [Fact, DisplayName("Deve bloquear a etapa HML quando as vulnerabilidades excederem o limite")]
        public async Task Hml_VulnerabilidadesAcimaDoLimite_DeveLancarExcecao()
        {
            _configRepo.GetBySistemaAsync("SISTEMA", Arg.Any<CancellationToken>()).Returns(Construtor.Configuracao(0));
            Deploys(Construtor.Deploy(1, branch: "HML").ComAnalise(severidades: "CRITICAL"));

            var act = () => new AmbienteHmlRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "HML"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("Vulnerabilidades críticas excederam o limite*");
        }

        [Fact, DisplayName("Deve lançar exceção quando a configuração do sistema não existir")]
        public async Task Hml_SemConfiguracao_DeveLancarExcecao()
        {
            _configRepo.GetBySistemaAsync("SISTEMA", Arg.Any<CancellationToken>()).Returns((ConfiguracaoSistemaEntity)null);
            Deploys(Construtor.Deploy(1, branch: "HML").ComAnalise());

            var act = () => new AmbienteHmlRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "HML"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("Configuração do sistema não encontrada");
        }

        [Fact, DisplayName("Não deve concluir deploys na regra de PRD, isso fica com o evento de conclusão do fluxo")]
        public async Task Prd_NaoDeveConcluirDeploys()
        {
            var deploy = Construtor.Deploy(1, branch: "PRD").ComAnalise();
            Deploys(deploy);

            await new AmbientePrdRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "PRD"));

            deploy.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve exigir ao menos um CRS na etapa de banco de dados")]
        public async Task BancoDeDados_SemCrs_DeveLancarExcecao()
        {
            var act = () => new BancoDeDadosRules(Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "BD"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("É necessário ter pelo menos um CRS*");
        }

        [Fact, DisplayName("Deve validar a etapa de banco de dados com CRS vinculado")]
        public async Task BancoDeDados_ComCrs_DeveValidar()
        {
            var fluxo = Construtor.Fluxo(1, "BD");
            fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/1");

            var act = () => new BancoDeDadosRules(Servicos).RegrasEtapaAsync(fluxo);

            await act.Should().NotThrowAsync();
        }

        [Fact, DisplayName("Deve exigir GMUD ou justificativa na etapa de GMUD")]
        public async Task Gmud_SemGmud_DeveLancarNotFound()
        {
            var act = () => new GmudRules(Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "GMUD"));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve exigir versão das tarefas na etapa de versão das tarefas")]
        public async Task VersaoTarefas_SemVersao_DeveLancarExcecao()
        {
            var act = () => new VersaoTarefasRules(Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "VERSAO"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("Versão das tarefas não cadastrada*");
        }

        [Fact, DisplayName("Deve lançar exceção listando as equipes técnicas sem aprovação")]
        public async Task EquipeTecnica_EquipesPendentes_DeveLancarExcecao()
        {
            var joao = Construtor.Aprovador(1, "INFRA");
            var fluxo = Construtor.Fluxo(1, "AET");
            fluxo.AdicionarAprovadorTecnico(joao, new[] { joao }, true, null);
            _aprovadorRepo.ObterEquipesTecnicasAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { "INFRA", "SEG" });
            _aprovadorRepo.ObterPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(new[] { joao });

            var act = () => new EquipeTecnicaRules(Servicos, _aprovadorRepo).RegrasEtapaAsync(fluxo);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não houve aprovação da(s) equipe(s): SEG.");
        }

        [Fact, DisplayName("Deve considerar pendente a equipe que reprovou")]
        public async Task EquipeTecnica_EquipeReprovou_DeveLancarExcecao()
        {
            var joao = Construtor.Aprovador(1, "INFRA");
            var fluxo = Construtor.Fluxo(1, "AET");
            fluxo.AdicionarAprovadorTecnico(joao, new[] { joao }, false, "reprovado");
            _aprovadorRepo.ObterEquipesTecnicasAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { "INFRA" });
            _aprovadorRepo.ObterPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Domain.AprovadoresTecnicos.Entities.AprovadorTecnicoEntity>());

            var act = () => new EquipeTecnicaRules(Servicos, _aprovadorRepo).RegrasEtapaAsync(fluxo);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não houve aprovação da(s) equipe(s): INFRA.");
        }

        [Fact, DisplayName("Deve validar a área de negócio quando todas as tarefas das tarefas estiverem aprovadas")]
        public async Task AreaNegocio_TarefasAprovadas_DeveValidar()
        {
            var fluxo = Construtor.Fluxo(1, "AAN");
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");
            _gerenciadorTarefas.ObterTarefas(Arg.Is<List<int>>(l => l.SequenceEqual(new[] { 10 })))
                .Returns(new List<TarefaDto> { new() { Id = 10, Status = "Aprovado" } });

            var act = () => new AreaNegocioRules(Servicos, _gerenciadorTarefas).RegrasEtapaAsync(fluxo);

            await act.Should().NotThrowAsync();
        }

        [Fact, DisplayName("Deve lançar exceção quando houver tarefa de negócio não aprovada")]
        public async Task AreaNegocio_TarefaPendente_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "AAN");
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");
            _gerenciadorTarefas.ObterTarefas(Arg.Any<List<int>>()).Returns(new List<TarefaDto> { new() { Id = 10, Status = "Pendente" } });

            var act = () => new AreaNegocioRules(Servicos, _gerenciadorTarefas).RegrasEtapaAsync(fluxo);

            await act.Should().ThrowAsync<DomainException>().WithMessage("As tarefas não estão aprovadas. IDs: 10");
        }

        [Fact, DisplayName("Deve bloquear a área de negócio sem nenhuma tarefa vinculada")]
        public async Task AreaNegocio_SemTarefas_DeveLancarExcecao()
        {
            var act = () => new AreaNegocioRules(Servicos, _gerenciadorTarefas).RegrasEtapaAsync(Construtor.Fluxo(1, "AAN"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("É necessário ter pelo menos uma tarefa vinculada*");
            await _gerenciadorTarefas.DidNotReceive().ObterTarefas(Arg.Any<List<int>>());
        }

        [Fact, DisplayName("Deve bloquear a área de negócio quando uma tarefa vinculada não retornar das tarefas")]
        public async Task AreaNegocio_TarefaNaoRetornada_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "AAN");
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");
            fluxo.AdicionarAprovadorNegocio(11, "https://tarefas.exemplo/issues/11");
            _gerenciadorTarefas.ObterTarefas(Arg.Any<List<int>>()).Returns(new List<TarefaDto> { new() { Id = 10, Status = "Aprovado" } });

            var act = () => new AreaNegocioRules(Servicos, _gerenciadorTarefas).RegrasEtapaAsync(fluxo);

            await act.Should().ThrowAsync<DomainException>().WithMessage("As tarefas não estão aprovadas. IDs: 11");
        }

        [Fact, DisplayName("Deve validar a abertura com versão no padrão")]
        public async Task Abertura_DeveValidar()
        {
            var act = () => new AberturaRules(Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "ABER"));

            await act.Should().NotThrowAsync();
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa da regra não estiver em execução")]
        public async Task Abertura_EtapaNaoExecutando_DeveLancarExcecao()
        {
            var fluxo = Construtor.Fluxo(1, "ABER", "DEV");
            fluxo.AvancarEtapa();

            var act = () => new AberturaRules(Servicos).RegrasEtapaAsync(fluxo);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa não está em EXECUÇÃO*");
        }

        [Fact, DisplayName("Deve validar a etapa de versão das tarefas com versão cadastrada")]
        public async Task VersaoTarefas_ComVersao_DeveValidar()
        {
            var fluxo = Construtor.Fluxo(1, "VERSAO");
            fluxo.CriarVersaoTarefas(1, 2);

            var act = () => new VersaoTarefasRules(Servicos).RegrasEtapaAsync(fluxo);

            await act.Should().NotThrowAsync();
        }

        [Theory, DisplayName("Deve validar a etapa de GMUD com GMUD vinculada ou somente com justificativa")]
        [InlineData("https://tarefas.exemplo/issues/7", null)]
        [InlineData(null, "sem gmud")]
        public async Task Gmud_ComGmudOuJustificativa_DeveValidar(string urlGmud, string justificativa)
        {
            var fluxo = Construtor.Fluxo(1, "GMUD");
            fluxo.CriarGmud(urlGmud, justificativa, "usuario");

            var act = () => new GmudRules(Servicos).RegrasEtapaAsync(fluxo);

            await act.Should().NotThrowAsync();
        }

        [Fact, DisplayName("Deve validar a etapa técnica quando todas as equipes aprovarem")]
        public async Task EquipeTecnica_TodasAprovadas_DeveValidar()
        {
            var joao = Construtor.Aprovador(1, "INFRA");
            var pedro = Construtor.Aprovador(2, "SEG");
            var fluxo = Construtor.Fluxo(1, "AET");
            fluxo.AdicionarAprovadorTecnico(joao, new[] { joao }, true, null);
            fluxo.AdicionarAprovadorTecnico(pedro, new[] { pedro }, true, null);
            _aprovadorRepo.ObterEquipesTecnicasAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { "INFRA", "SEG" });
            _aprovadorRepo.ObterPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(new[] { joao, pedro });

            var act = () => new EquipeTecnicaRules(Servicos, _aprovadorRepo).RegrasEtapaAsync(fluxo);

            await act.Should().NotThrowAsync();
        }

        [Fact, DisplayName("Deve bloquear a etapa DEV enquanto houver análise em execução")]
        public async Task Dev_AnaliseEmExecucao_DeveLancarExcecao()
        {
            Deploys(Construtor.Deploy(1, branch: "DEV").ComAnalise(status: StatusEnum.EXECUTANDO));

            var act = () => new AmbienteDevRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "DEV"));

            await act.Should().ThrowAsync<DomainException>().WithMessage("As seguintes análises ainda estão em execução: TRIVY");
        }

        public async Task Qa_DeveConcluirDeploysDaBranch()
        {
            var deployQa = Construtor.Deploy(1, branch: "QA").ComAnalise();
            var deployHml = Construtor.Deploy(2, branch: "HML").ComAnalise();
            Deploys(deployQa, deployHml);

            await new AmbienteQaRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "QA"));

            deployQa.Status.Should().Be(StatusEnum.CONCLUIDO);
            deployHml.Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve validar a etapa HML dentro do limite de vulnerabilidades e concluir os deploys da branch")]
        public async Task Hml_DentroDoLimite_DeveConcluirDeploys()
        {
            var deploy = Construtor.Deploy(1, branch: "HML").ComAnalise(severidades: "CRITICAL");
            Deploys(deploy);

            await new AmbienteHmlRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "HML"));

            deploy.Status.Should().Be(StatusEnum.CONCLUIDO);
        }

        [Fact, DisplayName("Deve bloquear a etapa PRD quando as vulnerabilidades excederem o limite")]
        public async Task Prd_VulnerabilidadesAcimaDoLimite_DeveLancarExcecao()
        {
            _configRepo.GetBySistemaAsync("SISTEMA", Arg.Any<CancellationToken>()).Returns(Construtor.Configuracao(0));
            Deploys(Construtor.Deploy(1, branch: "PRD").ComAnalise(severidades: "HIGH"));

            var act = () => new AmbientePrdRules(_deployRepo, _configRepo, Servicos).RegrasEtapaAsync(Construtor.Fluxo(1, "PRD"));

            await act.Should().ThrowAsync<DomainException>();
        }
    }
}
