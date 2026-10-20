using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.AprovadoresTecnicos.Entities
{
    public class AprovadorTecnicoEntityTests
    {
        [Fact, DisplayName("Deve criar o aprovador com os dados informados")]
        public void Criar_DeveManterDadosInformados()
        {
            var aprovador = AprovadorTecnicoEntity.Criar("usuario", "equipe", StatusEnum.ATIVO);

            aprovador.Usuario.Should().Be("usuario");
            aprovador.Equipe.Should().Be("equipe");
            aprovador.Status.Should().Be(StatusEnum.ATIVO);
        }

        [Theory, DisplayName("Deve lançar exceção quando o usuário for vazio")]
        [InlineData(null)]
        [InlineData("")]
        public void Criar_UsuarioVazio_DeveLancarDomainException(string usuario)
        {
            var act = () => AprovadorTecnicoEntity.Criar(usuario, "equipe", StatusEnum.ATIVO);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve atualizar o status do aprovador")]
        public void AtualizarStatus_DeveAtualizarStatus()
        {
            var aprovador = AprovadorTecnicoEntity.Criar("usuario", "equipe", StatusEnum.ATIVO);

            aprovador.AtualizarStatus(StatusEnum.CANCELADO);

            aprovador.Status.Should().Be(StatusEnum.CANCELADO);
        }
    }
}
