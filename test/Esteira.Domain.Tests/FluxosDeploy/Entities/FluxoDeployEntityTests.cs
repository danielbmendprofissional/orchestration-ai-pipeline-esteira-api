using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Events;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployEntityTests
    {
        private static readonly IReadOnlyList<EtapaEntity> Catalogo = new List<EtapaEntity>
        {
            EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true),
            EtapaEntity.Criar("QA", "Qualidade", 2, false),
            EtapaEntity.Criar("HML", "Homologação", 3, true),
            EtapaEntity.Criar("PRD", "Produção", 4, true)
        };

        private static FluxoDeployEntity CriarFluxo(string versaoAnterior = null, params string[] etapas)
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", versaoAnterior);
            fluxo.Id = 1;

            var siglas = etapas.Length == 0 ? new[] { "DEV", "QA", "HML", "PRD" } : etapas;

            foreach (var etapa in Catalogo.Where(c => siglas.Contains(c.Sigla)).OrderBy(c => c.Ordem))
                fluxo.AdicionarEtapa(etapa.Sigla, etapa.Ordem!.Value);

            return fluxo;
        }

        private static FluxoDeployEtapaEntity Etapa(FluxoDeployEntity fluxo, string sigla)
            => fluxo.Etapas.Single(e => e.SglEtapa == sigla);

        #region Criar

        [Fact, DisplayName("Deve criar o fluxo na versão v1 quando não houver versão anterior")]
        public void Criar_SemVersaoAnterior_DeveSerV1()
        {
            CriarFluxo().Versao.Should().Be("v1");
        }

        [Fact, DisplayName("Deve incrementar a versão anterior ao criar o fluxo")]
        public void Criar_ComVersaoAnterior_DeveIncrementar()
        {
            CriarFluxo("v3").Versao.Should().Be("v4");
        }

        [Fact, DisplayName("Deve criar o fluxo em execução, sem etapas e com a data de início informada")]
        public void Criar_DeveRegistrarStatusEInicio()
        {
            var agora = new DateTime(2026, 10, 13, 16, 0, 0);

            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null, agora: agora);

            fluxo.Status.Should().Be(StatusEnum.EXECUTANDO);
            fluxo.Inicio.Should().Be(agora);
            fluxo.Sistema.Should().Be("SISTEMA");
            fluxo.Usuario.Should().Be("usuario");
            fluxo.Etapas.Should().BeEmpty();
            fluxo.DomainEvents.Should().BeEmpty();
        }

        [Theory, DisplayName("Deve lançar exceção quando sistema ou usuário forem vazios")]
        [InlineData(null, "usuario")]
        [InlineData("", "usuario")]
        [InlineData("SISTEMA", null)]
        [InlineData("SISTEMA", "")]
        public void Criar_DadosVazios_DeveLancarDomainException(string sistema, string usuario)
        {
            var act = () => FluxoDeployEntity.Criar(sistema, usuario, null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a versão anterior for inválida")]
        public void Criar_VersaoAnteriorInvalida_DeveLancarDomainException()
        {
            var act = () => CriarFluxo("abc");

            act.Should().Throw<DomainException>();
        }

        #endregion

        #region AdicionarEtapa

        [Fact, DisplayName("Deve iniciar somente a etapa de menor ordem, independente da ordem de inclusão")]
        public void AdicionarEtapa_DeveIniciarEtapaDeMenorOrdem()
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null);

            fluxo.AdicionarEtapa("HML", 3);
            fluxo.AdicionarEtapa("DEV", 1);
            fluxo.AdicionarEtapa("QA", 2);

            Etapa(fluxo, "DEV").Status.Should().Be(StatusEnum.EXECUTANDO);
            Etapa(fluxo, "QA").Status.Should().Be(StatusEnum.NAO_INICIADO);
            Etapa(fluxo, "HML").Status.Should().Be(StatusEnum.NAO_INICIADO);
            fluxo.Etapas.Count(e => e.Status == StatusEnum.EXECUTANDO).Should().Be(1);
        }

        [Fact, DisplayName("Deve lançar exceção ao adicionar etapa já existente ignorando maiúsculas e minúsculas")]
        public void AdicionarEtapa_Duplicada_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV");

            var act = () => fluxo.AdicionarEtapa("dev", 2);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao adicionar etapa em uma ordem já ocupada")]
        public void AdicionarEtapa_OrdemDuplicada_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV");

            var act = () => fluxo.AdicionarEtapa("QA", 1);

            act.Should().Throw<DomainException>();
        }

        [Theory, DisplayName("Deve lançar exceção quando a sigla for vazia ou a ordem for inválida")]
        [InlineData(null, 1)]
        [InlineData("", 1)]
        [InlineData("DEV", 0)]
        [InlineData("DEV", -1)]
        public void AdicionarEtapa_DadosInvalidos_DeveLancarDomainException(string sigla, int ordem)
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null);

            var act = () => fluxo.AdicionarEtapa(sigla, ordem);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao adicionar etapa depois que alguma etapa foi concluída")]
        public void AdicionarEtapa_AposConclusao_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV", "QA");
            fluxo.AvancarEtapa();

            var act = () => fluxo.AdicionarEtapa("PRD", 4);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao adicionar etapa em fluxo que não está em execução")]
        public void AdicionarEtapa_FluxoNaoExecutando_DeveLancarDomainException()
        {
            var fluxo = FluxoDeployEntity.Criar("SISTEMA", "usuario", null, StatusEnum.CONCLUIDO);

            var act = () => fluxo.AdicionarEtapa("DEV", 1);

            act.Should().Throw<DomainException>();
        }

        #endregion

        #region AvancarEtapa / AtualizarEtapaEspecifica

        [Fact, DisplayName("Deve concluir a etapa atual e iniciar a seguinte ao avançar")]
        public void AvancarEtapa_DeveConcluirAtualEIniciarSeguinte()
        {
            var fluxo = CriarFluxo();

            fluxo.AvancarEtapa();

            Etapa(fluxo, "DEV").Status.Should().Be(StatusEnum.CONCLUIDO);
            Etapa(fluxo, "QA").Status.Should().Be(StatusEnum.EXECUTANDO);
            Etapa(fluxo, "HML").Status.Should().Be(StatusEnum.NAO_INICIADO);
        }

        [Fact, DisplayName("Deve concluir a última etapa sem exigir etapa seguinte")]
        public void AvancarEtapa_UltimaEtapa_DeveConcluir()
        {
            var fluxo = CriarFluxo(null, "PRD");

            fluxo.AvancarEtapa();

            Etapa(fluxo, "PRD").Status.Should().Be(StatusEnum.CONCLUIDO);
        }

        [Fact, DisplayName("Deve lançar exceção ao avançar sem etapa em execução")]
        public void AvancarEtapa_SemEtapaExecutando_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "PRD");
            fluxo.AvancarEtapa();

            var act = () => fluxo.AvancarEtapa();

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando a etapa não pertencer ao fluxo")]
        public void AtualizarEtapaEspecifica_EtapaInexistente_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV");

            var act = () => fluxo.AtualizarEtapaEspecifica("PRD");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao selecionar etapa sem nenhuma em execução")]
        public void AtualizarEtapaEspecifica_SemEtapaExecutando_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV");
            fluxo.AvancarEtapa();

            var act = () => fluxo.AtualizarEtapaEspecifica("DEV");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção ao selecionar etapa futura")]
        public void AtualizarEtapaEspecifica_EtapaFutura_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo();

            var act = () => fluxo.AtualizarEtapaEspecifica("HML");

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve reiniciar as etapas posteriores não canceladas e executar a selecionada")]
        public void AtualizarEtapaEspecifica_DeveReiniciarPosterioresEExecutarSelecionada()
        {
            var fluxo = CriarFluxo();
            fluxo.AvancarEtapa();
            Etapa(fluxo, "HML").Cancelar();

            fluxo.AtualizarEtapaEspecifica("DEV");

            Etapa(fluxo, "DEV").Status.Should().Be(StatusEnum.EXECUTANDO);
            Etapa(fluxo, "QA").Status.Should().Be(StatusEnum.NAO_INICIADO);
            Etapa(fluxo, "HML").Status.Should().Be(StatusEnum.CANCELADO);
            Etapa(fluxo, "PRD").Status.Should().Be(StatusEnum.NAO_INICIADO);
        }

        #endregion

        #region Concluir / Cancelar / Atualizar

        [Fact, DisplayName("Deve concluir o fluxo, registrar o fim e publicar o evento de fluxo concluído")]
        public void Concluir_DeveConcluirEPublicarEvento()
        {
            var fluxo = CriarFluxo();
            var fim = new DateTime(2026, 10, 13, 16, 0, 0);

            fluxo.Concluir(fim);

            fluxo.Status.Should().Be(StatusEnum.CONCLUIDO);
            fluxo.Fim.Should().Be(fim);
            fluxo.DomainEvents.Should().ContainSingle()
                .Which.Should().Be(new FluxoDeployConcluidoEvent(1, fim));
        }

        [Fact, DisplayName("Deve lançar exceção ao concluir um fluxo que não está em execução")]
        public void Concluir_FluxoConcluido_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo();
            fluxo.Concluir();

            var act = () => fluxo.Concluir();

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve cancelar o fluxo e as etapas mantendo a ordem e publicar o evento de fluxo cancelado")]
        public void Cancelar_DeveCancelarEtapasEPublicarEvento()
        {
            var fluxo = CriarFluxo(null, "DEV", "HML");
            var fim = new DateTime(2026, 10, 13, 16, 0, 0);

            fluxo.Cancelar(fim);

            fluxo.Status.Should().Be(StatusEnum.CANCELADO);
            fluxo.Fim.Should().Be(fim);
            fluxo.Etapas.Should().OnlyContain(e => e.Status == StatusEnum.CANCELADO && e.Ordem != null);
            fluxo.DomainEvents.Should().ContainSingle()
                .Which.Should().Be(new FluxoDeployCanceladoEvent(1, fim));
        }

        [Fact, DisplayName("Deve lançar exceção ao cancelar fluxo já cancelado ou concluído")]
        public void Cancelar_FluxoCanceladoOuConcluido_DeveLancarDomainException()
        {
            var cancelado = CriarFluxo();
            cancelado.Cancelar();
            var concluido = CriarFluxo();
            concluido.Concluir();

            var act1 = () => cancelado.Cancelar();
            var act2 = () => concluido.Cancelar();

            act1.Should().Throw<DomainException>();
            act2.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve limpar os eventos de domínio")]
        public void LimparEventos_DeveRemoverEventos()
        {
            var fluxo = CriarFluxo();
            fluxo.Concluir();

            fluxo.LimparEventos();

            fluxo.DomainEvents.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve atualizar versão, sistema e usuário com o fluxo em execução")]
        public void Atualizar_FluxoEmExecucao_DeveAtualizar()
        {
            var fluxo = CriarFluxo();

            fluxo.Atualizar("V5", "OUTRO", "outro.usuario");

            fluxo.Versao.Should().Be("v5");
            fluxo.Sistema.Should().Be("OUTRO");
            fluxo.Usuario.Should().Be("outro.usuario");
        }

        [Fact, DisplayName("Deve lançar exceção ao atualizar um fluxo que não está em execução")]
        public void Atualizar_FluxoConcluido_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo();
            fluxo.Concluir();

            var act = () => fluxo.Atualizar("v2", "SISTEMA", "usuario");

            act.Should().Throw<DomainException>();
        }

        #endregion

        #region CriarVersaoTarefas

        [Fact, DisplayName("Deve criar a versão das tarefas sem alterar as aprovações de negócio")]
        public void CriarVersaoTarefas_PrimeiroCadastro_DeveCriarVersao()
        {
            var fluxo = CriarFluxo();
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");

            fluxo.CriarVersaoTarefas(1, 2);

            fluxo.VersaoTarefas.IdProjeto.Should().Be(1);
            fluxo.VersaoTarefas.IdVersao.Should().Be(2);
            fluxo.AprovadoresNegocio.Should().ContainSingle();
        }

        [Fact, DisplayName("Deve manter as aprovações de negócio ao cadastrar a mesma versão das tarefas")]
        public void CriarVersaoTarefas_MesmaVersao_DeveManterAprovacoes()
        {
            var fluxo = CriarFluxo();
            fluxo.CriarVersaoTarefas(1, 2);
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");

            fluxo.CriarVersaoTarefas(1, 2);

            fluxo.AprovadoresNegocio.Should().ContainSingle();
        }

        [Theory, DisplayName("Deve remover as aprovações de negócio quando o projeto ou a versão das tarefas mudar")]
        [InlineData(1, 3)]
        [InlineData(5, 2)]
        [InlineData(5, 3)]
        public void CriarVersaoTarefas_VersaoDiferente_DeveRemoverAprovacoes(int idProjeto, int idVersao)
        {
            var fluxo = CriarFluxo();
            fluxo.CriarVersaoTarefas(1, 2);
            fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");
            fluxo.AdicionarAprovadorNegocio(11, "https://tarefas.exemplo/issues/11");

            fluxo.CriarVersaoTarefas(idProjeto, idVersao);

            fluxo.AprovadoresNegocio.Should().BeEmpty();
            fluxo.VersaoTarefas.IdProjeto.Should().Be(idProjeto);
            fluxo.VersaoTarefas.IdVersao.Should().Be(idVersao);
        }

        #endregion

        #region AtualizarFluxoDeployEtapas

        [Fact, DisplayName("Deve cancelar etapas removidas, manter as informadas e reorganizar a ordem")]
        public void AtualizarFluxoDeployEtapas_DeveReorganizar()
        {
            var fluxo = CriarFluxo(null, "DEV", "QA", "HML");

            fluxo.AtualizarFluxoDeployEtapas(Catalogo, new List<string> { "DEV", "HML", "PRD" });

            Etapa(fluxo, "DEV").Ordem.Should().Be(1);
            Etapa(fluxo, "HML").Ordem.Should().Be(2);
            Etapa(fluxo, "PRD").Ordem.Should().Be(3);
            Etapa(fluxo, "QA").Status.Should().Be(StatusEnum.CANCELADO);
            Etapa(fluxo, "QA").Ordem.Should().BeNull();
            Etapa(fluxo, "DEV").Status.Should().Be(StatusEnum.EXECUTANDO);
        }

        [Fact, DisplayName("Deve reconfigurar as etapas ignorando maiúsculas e minúsculas nas siglas informadas")]
        public void AtualizarFluxoDeployEtapas_SiglasMinusculas_DeveReorganizar()
        {
            var fluxo = CriarFluxo(null, "DEV", "QA", "HML");
            fluxo.AvancarEtapa();

            fluxo.AtualizarFluxoDeployEtapas(Catalogo, new List<string> { "dev", "hml", "prd" });

            Etapa(fluxo, "DEV").Status.Should().Be(StatusEnum.CONCLUIDO);
            Etapa(fluxo, "QA").Status.Should().Be(StatusEnum.CANCELADO);
            Etapa(fluxo, "HML").Ordem.Should().Be(2);
            Etapa(fluxo, "PRD").Ordem.Should().Be(3);
            fluxo.EtapaAtual().SglEtapa.Should().Be("HML");
        }

        [Fact, DisplayName("Deve selecionar a etapa específica ignorando maiúsculas e minúsculas")]
        public void AtualizarEtapaEspecifica_SiglaMinuscula_DeveSelecionar()
        {
            var fluxo = CriarFluxo();
            fluxo.AvancarEtapa();

            fluxo.AtualizarEtapaEspecifica("dev");

            fluxo.EtapaAtual().SglEtapa.Should().Be("DEV");
        }

        [Fact, DisplayName("Deve lançar exceção ao remover etapa já concluída")]
        public void AtualizarFluxoDeployEtapas_RemoverConcluida_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV", "HML");
            fluxo.AvancarEtapa();

            var act = () => fluxo.AtualizarFluxoDeployEtapas(Catalogo, new List<string> { "HML", "PRD" });

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando etapa obrigatória não for informada")]
        public void AtualizarFluxoDeployEtapas_ObrigatoriaAusente_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo(null, "DEV", "HML");

            var act = () => fluxo.AtualizarFluxoDeployEtapas(Catalogo, new List<string> { "DEV", "HML" });

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando as listas forem nulas")]
        public void AtualizarFluxoDeployEtapas_ListasNulas_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo();

            var act1 = () => fluxo.AtualizarFluxoDeployEtapas(null, new List<string>());
            var act2 = () => fluxo.AtualizarFluxoDeployEtapas(Catalogo, null);

            act1.Should().Throw<DomainException>();
            act2.Should().Throw<DomainException>();
        }

        #endregion

        #region Consultas de etapas

        [Fact, DisplayName("Deve retornar a etapa em execução como etapa atual")]
        public void EtapaAtual_DeveRetornarEtapaExecutando()
        {
            var fluxo = CriarFluxo();

            fluxo.EtapaAtual().SglEtapa.Should().Be("DEV");
        }

        [Fact, DisplayName("Deve retornar nulo como etapa atual quando nenhuma estiver em execução")]
        public void EtapaAtual_SemEtapaExecutando_DeveRetornarNulo()
        {
            var fluxo = CriarFluxo(null, "DEV");
            Etapa(fluxo, "DEV").Concluir();

            fluxo.EtapaAtual().Should().BeNull();
        }

        [Fact, DisplayName("Deve retornar a próxima etapa não iniciada na ordem")]
        public void ProximaEtapaNaoIniciada_DeveRetornarMenorOrdem()
        {
            var fluxo = CriarFluxo();

            fluxo.ProximaEtapaNaoIniciada().SglEtapa.Should().Be("QA");
        }

        [Fact, DisplayName("Deve retornar nulo quando não houver etapa não iniciada")]
        public void ProximaEtapaNaoIniciada_SemEtapas_DeveRetornarNulo()
        {
            var fluxo = CriarFluxo(null, "DEV");

            fluxo.ProximaEtapaNaoIniciada().Should().BeNull();
        }

        [Theory, DisplayName("Deve indicar se a etapa está em execução ignorando maiúsculas e minúsculas")]
        [InlineData("DEV", true)]
        [InlineData("dev", true)]
        [InlineData("QA", false)]
        [InlineData("XYZ", false)]
        public void EtapaEstaExecutando_DeveConsiderarSiglaEStatus(string sigla, bool esperado)
        {
            var fluxo = CriarFluxo();

            fluxo.EtapaEstaExecutando(sigla).Should().Be(esperado);
        }

        [Theory, DisplayName("Deve indicar se a etapa está concluída ignorando maiúsculas e minúsculas")]
        [InlineData("DEV", true)]
        [InlineData("dev", true)]
        [InlineData("QA", false)]
        [InlineData("XYZ", false)]
        public void EtapaEstaConcluida_DeveConsiderarSiglaEStatus(string sigla, bool esperado)
        {
            var fluxo = CriarFluxo();
            Etapa(fluxo, "DEV").Concluir();

            fluxo.EtapaEstaConcluida(sigla).Should().Be(esperado);
        }

        [Fact, DisplayName("Deve indicar quando alguma das etapas informadas estiver em execução")]
        public void AlgumaEtapaExecutando_DeveConsiderarQualquerSigla()
        {
            var fluxo = CriarFluxo();

            fluxo.AlgumaEtapaExecutando(new[] { "qa", "dev" }).Should().BeTrue();
            fluxo.AlgumaEtapaExecutando(new[] { "QA", "HML" }).Should().BeFalse();
            fluxo.AlgumaEtapaExecutando(new List<string>()).Should().BeFalse();
        }

        [Fact, DisplayName("Deve lançar exceção quando a lista de siglas for nula")]
        public void AlgumaEtapaExecutando_ListaNula_DeveLancarDomainException()
        {
            var fluxo = CriarFluxo();

            var act = () => fluxo.AlgumaEtapaExecutando(null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve retornar o status da etapa ignorando maiúsculas e minúsculas")]
        public void StatusEtapa_DeveRetornarStatusDaEtapa()
        {
            var fluxo = CriarFluxo();

            fluxo.StatusEtapa("dev").Should().Be(StatusEnum.EXECUTANDO);
            fluxo.StatusEtapa("QA").Should().Be(StatusEnum.NAO_INICIADO);
        }

        [Fact, DisplayName("Deve retornar o status padrão quando a etapa não existir no fluxo")]
        public void StatusEtapa_EtapaInexistente_DeveRetornarPadrao()
        {
            var fluxo = CriarFluxo();

            fluxo.StatusEtapa("XYZ").Should().Be(default(StatusEnum));
        }

        #endregion
    }
}
