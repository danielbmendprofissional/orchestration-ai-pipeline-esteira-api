using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Tests.Common;

namespace Esteira.Domain.Tests.FluxosDeploy.Entities
{
    public class FluxoDeployAprovadorTecnicoEntityTests
    {
        private static AprovadorTecnicoEntity Aprovador(int id, string equipe = "INFRA", StatusEnum status = StatusEnum.ATIVO)
        {
            var aprovador = AprovadorTecnicoEntity.Criar($"usuario{id}", equipe, status);
            aprovador.Id = id;
            return aprovador;
        }

        private static readonly AprovadorTecnicoEntity Joao = Aprovador(1);
        private static readonly AprovadorTecnicoEntity Maria = Aprovador(2);
        private static readonly AprovadorTecnicoEntity Pedro = Aprovador(3, "SEGURANCA");
        private static readonly List<AprovadorTecnicoEntity> Integrantes = new() { Joao, Maria };

        [Fact, DisplayName("Deve registrar a aprovação com a data de execução informada")]
        public void AdicionarAprovadorTecnico_DeveRegistrar()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            var agora = new DateTime(2026, 10, 13, 16, 0, 0);

            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, "ok", agora);

            var aprovacao = fluxo.AprovadoresTecnicos.Should().ContainSingle().Subject;
            aprovacao.IdAprovadorTecnico.Should().Be(1);
            aprovacao.Aprovado.Should().BeTrue();
            aprovacao.Execucao.Should().Be(agora);
            aprovacao.Justificativa.Should().Be("ok");
        }

        [Fact, DisplayName("Deve permitir decisões de equipes diferentes")]
        public void AdicionarAprovadorTecnico_EquipesDiferentes_DeveRegistrarAmbas()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");

            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);
            fluxo.AdicionarAprovadorTecnico(Pedro, new[] { Pedro }, true, null);

            fluxo.AprovadoresTecnicos.Should().HaveCount(2);
        }

        [Fact, DisplayName("Deve alterar o veredito quando o mesmo aprovador decidir novamente")]
        public void AdicionarAprovadorTecnico_MesmoAprovador_DeveAlterarVeredito()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            var agora = new DateTime(2026, 10, 19, 10, 0, 0);
            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, false, "reprovado");

            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null, agora);

            var aprovacao = fluxo.AprovadoresTecnicos.Should().ContainSingle().Subject;
            aprovacao.Aprovado.Should().BeTrue();
            aprovacao.Justificativa.Should().BeNull();
            aprovacao.Execucao.Should().Be(agora);
        }

        [Fact, DisplayName("Deve exigir justificativa ao alterar o veredito para reprovado")]
        public void AdicionarAprovadorTecnico_AlterarParaReprovadoSemJustificativa_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);

            var act = () => fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, false, null);

            act.Should().Throw<DomainException>();
            fluxo.AprovadoresTecnicos.Single().Aprovado.Should().BeTrue();
        }

        [Fact, DisplayName("Deve permitir alterar o veredito depois de voltar a etapa")]
        public void AdicionarAprovadorTecnico_AposVoltarEtapa_DeveAlterarVeredito()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET", "DEV");
            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);
            fluxo.AvancarEtapa();
            fluxo.AtualizarEtapaEspecifica("AET");

            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, false, "regressão");

            fluxo.AprovadoresTecnicos.Single().Aprovado.Should().BeFalse();
        }

        [Fact, DisplayName("Deve impedir aprovação técnica quando a etapa de aprovação técnica não estiver em execução")]
        public void AdicionarAprovadorTecnico_EtapaAetNaoExecutando_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "DEV", "AET");

            var act = () => fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);

            act.Should().Throw<DomainException>().WithMessage("A aprovação técnica só pode ser registrada com a etapa de aprovação técnica em execução.");
        }

        [Fact, DisplayName("Deve impedir o criador do fluxo de registrar aprovação técnica no próprio fluxo, ignorando maiúsculas e minúsculas")]
        public void AdicionarAprovadorTecnico_CriadorDoFluxo_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            var criador = AprovadorTecnicoEntity.Criar("USUARIO", "INFRA", StatusEnum.ATIVO);
            criador.Id = 9;

            var act = () => fluxo.AdicionarAprovadorTecnico(criador, new[] { criador }, true, null);

            act.Should().Throw<DomainException>().WithMessage("O criador do fluxo não pode registrar aprovação técnica no próprio fluxo.");
        }

        [Fact, DisplayName("Deve lançar exceção quando outro integrante da equipe já decidiu")]
        public void AdicionarAprovadorTecnico_EquipeJaDecidiu_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);

            var act = () => fluxo.AdicionarAprovadorTecnico(Maria, Integrantes, false, "reprovado");

            act.Should().Throw<DomainException>().WithMessage("A equipe já realizou uma decisão para este fluxo.");
        }

        [Theory, DisplayName("Deve exigir justificativa quando a aprovação for negada")]
        [InlineData(null)]
        [InlineData("")]
        public void AdicionarAprovadorTecnico_NegadaSemJustificativa_DeveLancarDomainException(string justificativa)
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");

            var act = () => fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, false, justificativa);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o aprovador estiver inativo")]
        public void AdicionarAprovadorTecnico_AprovadorInativo_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            var inativo = Aprovador(4, status: StatusEnum.INATIVO);

            var act = () => fluxo.AdicionarAprovadorTecnico(inativo, new[] { inativo }, true, null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o aprovador ou os integrantes forem nulos")]
        public void AdicionarAprovadorTecnico_Nulos_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");

            var act1 = () => fluxo.AdicionarAprovadorTecnico(null, Integrantes, true, null);
            var act2 = () => fluxo.AdicionarAprovadorTecnico(Joao, null, true, null);

            act1.Should().Throw<DomainException>();
            act2.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o aprovador não tiver id válido")]
        public void AdicionarAprovadorTecnico_IdInvalido_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            var semId = AprovadorTecnicoEntity.Criar("novo", "INFRA", StatusEnum.ATIVO);

            var act = () => fluxo.AdicionarAprovadorTecnico(semId, new[] { semId }, true, null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o fluxo não estiver em execução")]
        public void AdicionarAprovadorTecnico_FluxoConcluido_DeveLancarDomainException()
        {
            var fluxo = FluxoTestesSuporte.CriarFluxo(1, "AET");
            fluxo.Concluir();

            var act = () => fluxo.AdicionarAprovadorTecnico(Joao, Integrantes, true, null);

            act.Should().Throw<DomainException>();
        }
    }
}
