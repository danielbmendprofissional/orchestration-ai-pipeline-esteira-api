using Esteira.Domain.Common.Base;
using System.ComponentModel;
using AwesomeAssertions;
namespace Esteira.Domain.Tests.Common.Base
{
    public class EntityBaseTests
    {
        private sealed class EntidadeDeTeste : EntityBase { }

        [Fact, DisplayName("Deve nascer com PublicId GUID v7 antes de ser persistida")]
        public void Criar_DeveGerarPublicIdV7()
        {
            var entidade = new EntidadeDeTeste();

            entidade.PublicId.Should().NotBe(Guid.Empty);
            entidade.PublicId.Version.Should().Be(7);
        }

        [Fact, DisplayName("Deve deixar o Id interno para o banco atribuir")]
        public void Criar_IdDeveSerZero()
        {
            new EntidadeDeTeste().Id.Should().Be(0L);
        }

        // v7 ordena por milissegundo; dentro do mesmo milissegundo a ordem não é garantida.
        [Fact, DisplayName("Deve gerar PublicId diferente e em ordem de criação")]
        public void Criar_PublicIdsDevemSerUnicosEOrdenaveis()
        {
            var primeira = new EntidadeDeTeste();
            Thread.Sleep(2);
            var segunda = new EntidadeDeTeste();

            segunda.PublicId.Should().NotBe(primeira.PublicId);
            segunda.PublicId.CompareTo(primeira.PublicId).Should().BePositive();
        }
    }
}
