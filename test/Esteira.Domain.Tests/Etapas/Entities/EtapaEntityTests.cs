using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Entities;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Etapas.Entities
{
    public class EtapaEntityTests
    {
        [Fact, DisplayName("Deve criar a etapa ativa com os dados informados")]
        public void Criar_DeveCriarAtiva()
        {
            var etapa = EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true);

            etapa.Sigla.Should().Be("DEV");
            etapa.Descricao.Should().Be("Desenvolvimento");
            etapa.Ordem.Should().Be(1);
            etapa.Obrigatorio.Should().BeTrue();
            etapa.Status.Should().Be(StatusEnum.ATIVO);
        }

        [Theory, DisplayName("Deve lançar exceção quando a sigla for vazia")]
        [InlineData(null)]
        [InlineData("")]
        public void Criar_SiglaVazia_DeveLancarDomainException(string sigla)
        {
            var act = () => EtapaEntity.Criar(sigla, "descricao", 1, false);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve atualizar o status da etapa")]
        public void AtualizarStatusEtapa_DeveAtualizarStatus()
        {
            var etapa = EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true);

            etapa.AtualizarStatusEtapa(StatusEnum.CANCELADO);

            etapa.Status.Should().Be(StatusEnum.CANCELADO);
        }
    }
}
