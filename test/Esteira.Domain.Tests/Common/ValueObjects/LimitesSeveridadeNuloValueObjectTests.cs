using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Common.ValueObjects;

namespace Esteira.Domain.Tests.Common.ValueObjects
{
    // Critério 11 da A1: limite null = sem limite; negativo continua recusado.
    [Trait("Fatia", "A1")]
    public class LimitesSeveridadeNuloValueObjectTests
    {
        private static ResumoSeveridadeValueObject Resumo(int c, int a, int m, int b) => ResumoSeveridadeValueObject.Criar(c, a, m, b).Value;

        [Fact, DisplayName("Deve aceitar null em cada severidade")]
        public void Criar_Nulo_DeveSerSucesso()
        {
            var resultado = LimitesSeveridadeValueObject.Criar(null, null, null, null);

            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.VulnerabilidadeCritica.Should().BeNull();
            resultado.Value.VulnerabilidadeBaixa.Should().BeNull();
        }

        [Fact, DisplayName("Deve ignorar a severidade sem limite em ObterExcessos")]
        public void ObterExcessos_SeveridadeNula_NaoExcede()
        {
            var limites = LimitesSeveridadeValueObject.Criar(0, 0, null, null).Value;

            limites.ObterExcessos(Resumo(0, 0, 999, 999)).Should().BeEmpty();
        }

        [Fact, DisplayName("Deve continuar apontando excesso nas severidades com limite")]
        public void ObterExcessos_MistoNuloELimite_ApontaSoAsComLimite()
        {
            var limites = LimitesSeveridadeValueObject.Criar(0, null, 1, null).Value;

            var erros = limites.ObterExcessos(Resumo(1, 50, 2, 50));

            erros.Should().HaveCount(2);
            erros[0].Should().Contain("críticas");
            erros[1].Should().Contain("médias");
        }

        [Theory, DisplayName("Deve recusar negativo mesmo com outras severidades null")]
        [InlineData(-1, null, null, null)]
        [InlineData(null, -1, null, null)]
        [InlineData(null, null, -1, null)]
        [InlineData(null, null, null, -1)]
        public void Criar_NegativoComNulos_DeveFalhar(int? c, int? a, int? m, int? b)
        {
            var resultado = LimitesSeveridadeValueObject.Criar(c, a, m, b);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.limite-negativo");
        }

        [Fact, DisplayName("Deve ser igual quando os limites null coincidem")]
        public void Equals_Nulos_DeveSerIgual()
        {
            LimitesSeveridadeValueObject.Criar(null, 1, null, 2).Value
                .Should().Be(LimitesSeveridadeValueObject.Criar(null, 1, null, 2).Value);
        }
    }
}
