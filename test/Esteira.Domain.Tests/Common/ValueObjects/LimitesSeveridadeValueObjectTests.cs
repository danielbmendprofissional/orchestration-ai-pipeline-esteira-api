using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.ValueObjects;
using AwesomeAssertions;
using System.ComponentModel;

namespace Esteira.Domain.Tests.Common.ValueObjects
{
    public class LimitesSeveridadeValueObjectTests
    {
        private static LimitesSeveridadeValueObject Limites(int c, int a, int m, int b) => LimitesSeveridadeValueObject.Criar(c, a, m, b).Value;
        private static ResumoSeveridadeValueObject Resumo(int c, int a, int m, int b) => ResumoSeveridadeValueObject.Criar(c, a, m, b).Value;

        [Fact, DisplayName("Deve aceitar resumo igual ao limite")]
        public void ObterExcessos_IgualAoLimite_NaoDeveExceder()
        {
            var limites = Limites(1, 2, 3, 4);

            limites.ObterExcessos(Resumo(1, 2, 3, 4)).Should().BeEmpty();
        }

        [Fact, DisplayName("Deve rejeitar qualquer vulnerabilidade quando o limite for nenhum")]
        public void ObterExcessos_LimiteNenhum_QualquerVulnerabilidadeExcede()
        {
            LimitesSeveridadeValueObject.Nenhum.ObterExcessos(Resumo(0, 0, 0, 1)).Should().HaveCount(1);
            LimitesSeveridadeValueObject.Nenhum.ObterExcessos(ResumoSeveridadeValueObject.Vazio).Should().BeEmpty();
        }

        [Fact, DisplayName("Deve retornar uma mensagem para cada severidade que excedeu o limite")]
        public void ObterExcessos_DeveRetornarMensagemPorSeveridade()
        {
            var limites = Limites(0, 5, 5, 5);

            var erros = limites.ObterExcessos(Resumo(2, 6, 5, 0));

            erros.Should().HaveCount(2);
            erros[0].Should().Be("Vulnerabilidades críticas excederam o limite. Atual: 2, Limite: 0");
            erros[1].Should().Be("Vulnerabilidades altas excederam o limite. Atual: 6, Limite: 5");
        }

        [Theory, DisplayName("Deve devolver falha politica.limite-negativo quando algum limite for negativo")]
        [InlineData(-1, 0, 0, 0)]
        [InlineData(0, -1, 0, 0)]
        [InlineData(0, 0, -1, 0)]
        [InlineData(0, 0, 0, -1)]
        public void Criar_Negativo_DeveDevolverFalha(int critica, int alta, int media, int baixa)
        {
            var resultado = LimitesSeveridadeValueObject.Criar(critica, alta, media, baixa);

            resultado.IsSuccess.Should().BeFalse();
            resultado.Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "politica.limite-negativo" && e.Type == TipoErro.Validation);
        }

        [Fact, DisplayName("Deve lançar ArgumentNullException quando o resumo for nulo")]
        public void ObterExcessos_ResumoNulo_DeveLancarArgumentNull()
        {
            var act = () => LimitesSeveridadeValueObject.Nenhum.ObterExcessos(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact, DisplayName("Deve ser igual quando todos os limites forem iguais")]
        public void Equals_MesmosValores_DeveSerIgual()
        {
            Limites(1, 2, 3, 4).Should().Be(Limites(1, 2, 3, 4));
        }
    }
}
