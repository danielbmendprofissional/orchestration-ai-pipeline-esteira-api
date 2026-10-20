using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.ConfiguracoesSistema.ValueObjects;
using AwesomeAssertions;
using System.ComponentModel;

namespace Esteira.Domain.Tests.ConfiguracoesSistema.ValueObjects
{
    public class LimitesSeveridadeValueObjectTests
    {
        [Fact, DisplayName("Deve aceitar resumo igual ao limite")]
        public void ObterExcessos_IgualAoLimite_NaoDeveExceder()
        {
            var limites = new LimitesSeveridadeValueObject(1, 2, 3, 4);

            limites.ObterExcessos(new ResumoSeveridadeValueObject(1, 2, 3, 4)).Should().BeEmpty();
        }

        [Fact, DisplayName("Deve rejeitar qualquer vulnerabilidade quando o limite for nenhum")]
        public void ObterExcessos_LimiteNenhum_QualquerVulnerabilidadeExcede()
        {
            LimitesSeveridadeValueObject.Nenhum.ObterExcessos(new ResumoSeveridadeValueObject(0, 0, 0, 1)).Should().HaveCount(1);
            LimitesSeveridadeValueObject.Nenhum.ObterExcessos(ResumoSeveridadeValueObject.Vazio).Should().BeEmpty();
        }

        [Fact, DisplayName("Deve retornar uma mensagem para cada severidade que excedeu o limite")]
        public void ObterExcessos_DeveRetornarMensagemPorSeveridade()
        {
            var limites = new LimitesSeveridadeValueObject(0, 5, 5, 5);

            var erros = limites.ObterExcessos(new ResumoSeveridadeValueObject(2, 6, 5, 0));

            erros.Should().HaveCount(2);
            erros[0].Should().Be("Vulnerabilidades críticas excederam o limite. Atual: 2, Limite: 0");
            erros[1].Should().Be("Vulnerabilidades altas excederam o limite. Atual: 6, Limite: 5");
        }

        [Fact, DisplayName("Deve lançar exceção quando algum limite for negativo")]
        public void Criar_Negativo_DeveLancarDomainException()
        {
            var act = () => new LimitesSeveridadeValueObject(-1, 0, 0, 0);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve lançar exceção quando o resumo for nulo")]
        public void ObterExcessos_ResumoNulo_DeveLancarDomainException()
        {
            var act = () => LimitesSeveridadeValueObject.Nenhum.ObterExcessos(null);

            act.Should().Throw<DomainException>();
        }

        [Fact, DisplayName("Deve ser igual quando todos os limites forem iguais")]
        public void Equals_MesmosValores_DeveSerIgual()
        {
            new LimitesSeveridadeValueObject(1, 2, 3, 4).Should().Be(new LimitesSeveridadeValueObject(1, 2, 3, 4));
        }
    }
}
