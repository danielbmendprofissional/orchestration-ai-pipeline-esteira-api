using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Tests.Common;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Events;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Cenarios
{
    public class FluxoDeployCenariosTests
    {
        private readonly Cenario _cenario = new();

        [Fact, DisplayName("Deve percorrer todas as etapas até concluir o fluxo e publicar o evento de conclusão")]
        public async Task CaminhoFeliz_DeveConcluirFluxo()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.PrepararTodasAsEtapas(fluxo.Id);
            await _cenario.AvancarAte(fluxo.Id, "AET");
            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario1", true);
            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario3", true);

            while (fluxo.Status == StatusEnum.EXECUTANDO)
                await _cenario.Avancar(fluxo.Id);

            fluxo.Status.Should().Be(StatusEnum.CONCLUIDO);
            fluxo.Etapas.Should().OnlyContain(e => e.Status == StatusEnum.CONCLUIDO);
            fluxo.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FluxoDeployConcluidoEvent>();
            _cenario.DeploysDoFluxo(fluxo.Id).Where(d => d.Branch != "PRD").Should().OnlyContain(d => d.Status == StatusEnum.CONCLUIDO);
            _cenario.DeploysDoFluxo(fluxo.Id).Single(d => d.Branch == "PRD").Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve bloquear a etapa de versão das tarefas sem versão e avançar depois de cadastrar a versão")]
        public async Task VersaoTarefas_SemVersao_DeveBloquearAteCadastrar()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.Avancar(fluxo.Id);

            var act = () => _cenario.Avancar(fluxo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Versão das tarefas não cadastrada*");
            _cenario.StatusEtapa(fluxo.Id, "VERSAO").Should().Be(StatusEnum.EXECUTANDO);

            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "VERSAO").Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(fluxo.Id, "GMUD").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve bloquear a etapa de GMUD sem GMUD e avançar com justificativa")]
        public async Task Gmud_SemGmud_DeveBloquearAteJustificar()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.AvancarAte(fluxo.Id, "GMUD");

            var act = () => _cenario.Avancar(fluxo.Id);

            await act.Should().ThrowAsync<NotFoundException>();
            _cenario.StatusEtapa(fluxo.Id, "GMUD").Should().Be(StatusEnum.EXECUTANDO);

            await _cenario.Gmud(fluxo.Id, null, "sem gmud");
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "GMUD").Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(fluxo.Id, "BD").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve bloquear a etapa de banco de dados sem CRS e avançar depois de vincular")]
        public async Task BancoDeDados_SemCrs_DeveBloquearAteVincular()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.Gmud(fluxo.Id, 7);
            await _cenario.AvancarAte(fluxo.Id, "BD");

            var act = () => _cenario.Avancar(fluxo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("É necessário ter pelo menos um CRS*");

            await _cenario.Crs(fluxo.Id, "CRS-1");
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "AET").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve bloquear a etapa técnica enquanto alguma equipe não aprovar")]
        public async Task EquipeTecnica_EquipePendente_DeveBloquearAteAprovar()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "AET", "DEV", "HML", "PRD");
            await _cenario.AvancarAte(fluxo.Id, "AET");

            var semAprovacao = () => _cenario.Avancar(fluxo.Id);
            await semAprovacao.Should().ThrowAsync<DomainException>().WithMessage("Não houve aprovação da(s) equipe(s): INFRA, SEG.");

            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario2", true);
            var faltandoSeg = () => _cenario.Avancar(fluxo.Id);
            await faltandoSeg.Should().ThrowAsync<DomainException>().WithMessage("Não houve aprovação da(s) equipe(s): SEG.");

            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario3", true);
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "DEV").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve manter a etapa técnica bloqueada quando a equipe reprovar e impedir nova decisão da mesma equipe")]
        public async Task EquipeTecnica_EquipeReprovou_DeveBloquear()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "AET", "DEV", "HML", "PRD");
            await _cenario.AvancarAte(fluxo.Id, "AET");
            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario1", true);
            await _cenario.AprovacaoTecnica(fluxo.Id, "usuario3", false, "reprovado");

            var avancar = () => _cenario.Avancar(fluxo.Id);
            await avancar.Should().ThrowAsync<DomainException>().WithMessage("Não houve aprovação da(s) equipe(s): SEG.");

            var novaDecisao = () => _cenario.AprovacaoTecnica(fluxo.Id, "usuario2", true);
            await novaDecisao.Should().ThrowAsync<DomainException>().WithMessage("A equipe já realizou uma decisão para este fluxo.");
        }

        [Fact, DisplayName("Deve impedir aprovação técnica antes da etapa de aprovação técnica e pelo criador do fluxo")]
        public async Task EquipeTecnica_ForaDaEtapaOuPeloCriador_DeveLancarExcecao()
        {
            _cenario.Aprovadores.Add(AprovadorTecnicoEntity.Criar("usuario", "QUALIDADE", StatusEnum.ATIVO).ComId(9));
            var fluxo = await _cenario.CriarFluxo("ABER", "AET", "DEV", "HML", "PRD");

            var antes = () => _cenario.AprovacaoTecnica(fluxo.Id, "usuario1", true);
            await antes.Should().ThrowAsync<DomainException>().WithMessage("A aprovação técnica só pode ser registrada*");

            await _cenario.AvancarAte(fluxo.Id, "AET");
            var criador = () => _cenario.AprovacaoTecnica(fluxo.Id, "usuario", true);
            await criador.Should().ThrowAsync<DomainException>().WithMessage("O criador do fluxo não pode registrar aprovação técnica no próprio fluxo.");
        }

        [Fact, DisplayName("Deve bloquear o ambiente de desenvolvimento sem deploy e com análise ainda em execução")]
        public async Task Dev_SemDeployOuAnaliseExecutando_DeveBloquear()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            await _cenario.AvancarAte(fluxo.Id, "DEV");

            var semDeploy = () => _cenario.Avancar(fluxo.Id);
            await semDeploy.Should().ThrowAsync<DomainException>().WithMessage("Não há deploys válidos para o ambiente de desenvolvimento");

            _cenario.Deploy(fluxo.Id, "DEV", StatusEnum.EXECUTANDO);
            var analiseExecutando = () => _cenario.Avancar(fluxo.Id);
            await analiseExecutando.Should().ThrowAsync<DomainException>().WithMessage("As seguintes análises ainda estão em execução: TRIVY");

            _cenario.StatusEtapa(fluxo.Id, "DEV").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve desconsiderar deploys de outra branch na validação do ambiente")]
        public async Task Dev_DeployDeOutraBranch_DeveBloquear()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(fluxo.Id, "HML");
            await _cenario.AvancarAte(fluxo.Id, "DEV");

            var act = () => _cenario.Avancar(fluxo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não há deploys válidos para o ambiente de desenvolvimento");
        }

        [Fact, DisplayName("Deve bloquear homologação quando as vulnerabilidades excederem o limite do sistema")]
        public async Task Hml_VulnerabilidadeAcimaDoLimite_DeveBloquear()
        {
            _cenario.Configuracao = Construtor.Configuracao(0);
            var fluxo = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(fluxo.Id, "DEV");
            _cenario.Deploy(fluxo.Id, "HML", StatusEnum.CONCLUIDO, "CRITICAL");
            await _cenario.AvancarAte(fluxo.Id, "HML");

            var act = () => _cenario.Avancar(fluxo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Vulnerabilidades críticas excederam o limite*");
            _cenario.StatusEtapa(fluxo.Id, "HML").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve bloquear a área de negócio enquanto houver tarefa não aprovada no gerenciador de tarefas")]
        public async Task AreaNegocio_TarefaPendente_DeveBloquearAteAprovar()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "DEV", "HML", "AAN", "PRD");
            _cenario.Deploy(fluxo.Id, "DEV");
            _cenario.Deploy(fluxo.Id, "HML");
            await _cenario.AprovacoesNegocio(fluxo.Id, 10, 11);
            _cenario.StatusTarefa(10, "Aprovado");
            await _cenario.AvancarAte(fluxo.Id, "AAN");

            var act = () => _cenario.Avancar(fluxo.Id);
            await act.Should().ThrowAsync<DomainException>().WithMessage("As tarefas não estão aprovadas. IDs: 11");

            _cenario.StatusTarefa(11, "Aprovado");
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "PRD").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve voltar para uma etapa anterior mantendo concluídas as etapas já concluídas e os recursos vinculados")]
        public async Task Voltar_DeveReiniciarSomenteEtapaEmExecucaoEManterRecursos()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.Gmud(fluxo.Id, 7);
            await _cenario.AvancarAte(fluxo.Id, "BD");

            await _cenario.Voltar(fluxo.Id, "versao");

            _cenario.StatusEtapa(fluxo.Id, "ABER").Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(fluxo.Id, "VERSAO").Should().Be(StatusEnum.EXECUTANDO);
            _cenario.StatusEtapa(fluxo.Id, "GMUD").Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(fluxo.Id, "BD").Should().Be(StatusEnum.NAO_INICIADO);
            fluxo.Etapas.Count(e => e.Status == StatusEnum.EXECUTANDO).Should().Be(1);

            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "VERSAO").Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(fluxo.Id, "BD").Should().Be(StatusEnum.EXECUTANDO);
            fluxo.VersaoTarefas.Should().NotBeNull();
            fluxo.Gmud.Should().NotBeNull();
        }

        [Fact, DisplayName("Deve remover as aprovações de negócio ao voltar e cadastrar uma nova versão das tarefas")]
        public async Task Voltar_NovaVersaoTarefas_DeveRemoverAprovacoesDeNegocio()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.AprovacoesNegocio(fluxo.Id, 10, 11);
            await _cenario.Gmud(fluxo.Id, 7);
            await _cenario.AvancarAte(fluxo.Id, "BD");

            await _cenario.Voltar(fluxo.Id, "VERSAO");
            await _cenario.VersaoTarefas(fluxo.Id, 1, 3);

            fluxo.AprovadoresNegocio.Should().BeEmpty();
            fluxo.VersaoTarefas.IdVersao.Should().Be(3);
        }

        [Fact, DisplayName("Deve bloquear a área de negócio após nova versão das tarefas até vincular e aprovar novas tarefas")]
        public async Task Voltar_NovaVersaoTarefas_DeveBloquearAreaNegocioAteNovasAprovacoes()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "VERSAO", "DEV", "HML", "AAN", "PRD");
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.AprovacoesNegocio(fluxo.Id, 10);
            _cenario.StatusTarefa(10, "Aprovado");
            _cenario.Deploy(fluxo.Id, "DEV");
            _cenario.Deploy(fluxo.Id, "HML");
            await _cenario.AvancarAte(fluxo.Id, "DEV");

            await _cenario.Voltar(fluxo.Id, "VERSAO");
            await _cenario.VersaoTarefas(fluxo.Id, 1, 3);
            await _cenario.AvancarAte(fluxo.Id, "AAN");

            var semTarefas = () => _cenario.Avancar(fluxo.Id);
            await semTarefas.Should().ThrowAsync<DomainException>().WithMessage("É necessário ter pelo menos uma tarefa vinculada*");

            await _cenario.AprovacoesNegocio(fluxo.Id, 20);
            var pendente = () => _cenario.Avancar(fluxo.Id);
            await pendente.Should().ThrowAsync<DomainException>().WithMessage("As tarefas não estão aprovadas. IDs: 20");

            _cenario.StatusTarefa(20, "Aprovado");
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "PRD").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve manter as aprovações de negócio ao voltar e cadastrar a mesma versão das tarefas")]
        public async Task Voltar_MesmaVersaoTarefas_DeveManterAprovacoesDeNegocio()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.AprovacoesNegocio(fluxo.Id, 10, 11);
            await _cenario.AvancarAte(fluxo.Id, "GMUD");

            await _cenario.Voltar(fluxo.Id, "VERSAO");
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);

            fluxo.AprovadoresNegocio.Select(a => a.IdTarefa).Should().BeEquivalentTo(new[] { 10, 11 });
        }

        [Fact, DisplayName("Deve impedir voltar para uma etapa futura")]
        public async Task Voltar_EtapaFutura_DeveLancarExcecao()
        {
            var fluxo = await _cenario.CriarFluxo();

            var act = () => _cenario.Voltar(fluxo.Id, "GMUD");

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não é possível selecionar uma etapa futura!");
            _cenario.StatusEtapa(fluxo.Id, "ABER").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve pular a etapa removida na reconfiguração ao avançar")]
        public async Task Reconfigurar_EtapaRemovida_DeveSerPulada()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "DEV", "QA", "HML", "PRD");
            _cenario.Deploy(fluxo.Id, "DEV");
            await _cenario.AvancarAte(fluxo.Id, "DEV");

            await _cenario.Reconfigurar(fluxo.Id, "ABER", "DEV", "HML", "PRD");
            await _cenario.Avancar(fluxo.Id);

            _cenario.StatusEtapa(fluxo.Id, "QA").Should().Be(StatusEnum.CANCELADO);
            _cenario.StatusEtapa(fluxo.Id, "HML").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve impedir remover etapa já concluída na reconfiguração")]
        public async Task Reconfigurar_RemoverEtapaConcluida_DeveLancarExcecao()
        {
            var fluxo = await _cenario.CriarFluxo("ABER", "VERSAO", "DEV", "HML", "PRD");
            await _cenario.VersaoTarefas(fluxo.Id, 1, 2);
            await _cenario.AvancarAte(fluxo.Id, "DEV");

            var act = () => _cenario.Reconfigurar(fluxo.Id, "ABER", "DEV", "HML", "PRD");

            await act.Should().ThrowAsync<DomainException>().WithMessage("Não é permitido remover etapas já concluídas.");
        }

        [Fact, DisplayName("Deve impedir qualquer alteração depois que o fluxo for cancelado")]
        public async Task Cancelado_DeveImpedirAlteracoes()
        {
            var fluxo = await _cenario.CriarFluxo();
            await _cenario.Cancelar(fluxo.Id);

            var avancar = () => _cenario.Avancar(fluxo.Id);
            var gmud = () => _cenario.Gmud(fluxo.Id, 7);
            var voltar = () => _cenario.Voltar(fluxo.Id, "ABER");

            await avancar.Should().ThrowAsync<DomainException>().WithMessage("*não está em execução");
            await gmud.Should().ThrowAsync<DomainException>().WithMessage("*não está em execução");
            await voltar.Should().ThrowAsync<DomainException>().WithMessage("*não está em execução");
            fluxo.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FluxoDeployCanceladoEvent>();
        }

        [Fact, DisplayName("Deve impedir criar um segundo fluxo antes do atual chegar em homologação")]
        public async Task SegundoFluxo_AtualAntesDeHml_DeveLancarExcecao()
        {
            await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");

            var act = () => _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");

            await act.Should().ThrowAsync<DomainException>().WithMessage("*sem que a atual esteja pelo menos em homologação");
        }

        [Fact, DisplayName("Deve criar o segundo fluxo com versão seguinte e bloquear sua entrada em homologação enquanto o primeiro estiver lá")]
        public async Task SegundoFluxo_PrimeiroEmHml_DeveBloquearQa()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            await _cenario.AvancarAte(primeiro.Id, "HML");

            var segundo = await _cenario.CriarFluxo("ABER", "DEV", "QA", "HML", "PRD");
            _cenario.Deploy(segundo.Id, "DEV");
            _cenario.Deploy(segundo.Id, "QA");
            await _cenario.AvancarAte(segundo.Id, "QA");

            var act = () => _cenario.Avancar(segundo.Id);

            segundo.Versao.Should().Be("v2");
            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 1");
            _cenario.StatusEtapa(segundo.Id, "QA").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve impedir criar o segundo fluxo começando direto na etapa em que o primeiro está")]
        public async Task SegundoFluxo_ComecandoNaEtapaDoPrimeiro_DeveLancarExcecao()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            await _cenario.AvancarAte(primeiro.Id, "HML");

            var act = () => _cenario.CriarFluxo("HML", "PRD");

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 1");
            await _cenario.FluxoRepo.Received(1).AddAsync(Arg.Any<FluxoDeployEntity>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve bloquear o segundo fluxo sem DEV e QA de sair da abertura para homologação enquanto o primeiro estiver lá")]
        public async Task SegundoFluxo_SemAmbientesTecnicos_DeveBloquearEntradaEmHml()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            await _cenario.AvancarAte(primeiro.Id, "HML");
            var segundo = await _cenario.CriarFluxo("ABER", "HML", "PRD");

            var act = () => _cenario.Avancar(segundo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa HML já está em execução no fluxo de deploy 1");
            _cenario.StatusEtapa(segundo.Id, "ABER").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve permitir o segundo fluxo entrar em homologação com o primeiro em produção e bloquear a entrada em produção")]
        public async Task SegundoFluxo_PrimeiroEmPrd_DevePermitirHmlEBloquearPrd()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            _cenario.Deploy(primeiro.Id, "HML");
            await _cenario.AvancarAte(primeiro.Id, "PRD");
            var segundo = await _cenario.CriarFluxo("ABER", "HML", "PRD");
            _cenario.Deploy(segundo.Id, "HML");

            await _cenario.Avancar(segundo.Id);
            var act = () => _cenario.Avancar(segundo.Id);

            _cenario.StatusEtapa(segundo.Id, "HML").Should().Be(StatusEnum.EXECUTANDO);
            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa PRD já está em execução no fluxo de deploy 1");
        }

        [Fact, DisplayName("Deve bloquear o segundo fluxo de entrar na área de negócio enquanto o primeiro estiver nela")]
        public async Task SegundoFluxo_PrimeiroEmAan_DeveBloquearAan()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "AAN", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            _cenario.Deploy(primeiro.Id, "HML");
            await _cenario.AvancarAte(primeiro.Id, "AAN");
            var segundo = await _cenario.CriarFluxo("ABER", "HML", "AAN", "PRD");
            _cenario.Deploy(segundo.Id, "HML");
            await _cenario.AvancarAte(segundo.Id, "HML");

            var act = () => _cenario.Avancar(segundo.Id);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa AAN já está em execução no fluxo de deploy 1");
            _cenario.StatusEtapa(segundo.Id, "HML").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve impedir o segundo fluxo de ultrapassar o primeiro em produção e liberar após a conclusão do primeiro")]
        public async Task SegundoFluxo_Ultrapassagem_DeveBloquearPrdAteConcluirPrimeiro()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "AAN", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            _cenario.Deploy(primeiro.Id, "HML");
            _cenario.Deploy(primeiro.Id, "PRD");
            await _cenario.AprovacoesNegocio(primeiro.Id, 10);
            _cenario.StatusTarefa(10, "Aprovado");
            await _cenario.AvancarAte(primeiro.Id, "AAN");
            var segundo = await _cenario.CriarFluxo("ABER", "HML", "PRD");
            _cenario.Deploy(segundo.Id, "HML");
            await _cenario.AvancarAte(segundo.Id, "HML");

            var ultrapassar = () => _cenario.Avancar(segundo.Id);
            await ultrapassar.Should().ThrowAsync<DomainException>().WithMessage("Não é possível iniciar a etapa de PRODUÇÃO antes da conclusão do fluxo de deploy 1");

            await _cenario.Avancar(primeiro.Id);
            await _cenario.Avancar(primeiro.Id);
            await _cenario.Avancar(segundo.Id);

            primeiro.Status.Should().Be(StatusEnum.CONCLUIDO);
            _cenario.StatusEtapa(segundo.Id, "PRD").Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve impedir um terceiro fluxo em execução para o mesmo sistema")]
        public async Task TerceiroFluxo_DeveLancarExcecao()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            await _cenario.AvancarAte(primeiro.Id, "HML");
            await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");

            var act = () => _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");

            await act.Should().ThrowAsync<DomainException>().WithMessage("Já existem 2 fluxos em EXECUÇÃO para a aplicação SISTEMA");
        }

        [Fact, DisplayName("Deve impedir voltar o fluxo em homologação para etapa técnica quando houver outro fluxo em execução")]
        public async Task Voltar_ComOutroFluxo_DeveImpedirSairDosAmbientesFinais()
        {
            var primeiro = await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");
            _cenario.Deploy(primeiro.Id, "DEV");
            await _cenario.AvancarAte(primeiro.Id, "HML");
            await _cenario.CriarFluxo("ABER", "DEV", "HML", "PRD");

            var act = () => _cenario.Voltar(primeiro.Id, "DEV");

            await act.Should().ThrowAsync<DomainException>().WithMessage("A etapa não pode ser selecionada pois existe outro fluxo*");
            _cenario.StatusEtapa(primeiro.Id, "HML").Should().Be(StatusEnum.EXECUTANDO);
        }
    }
}
