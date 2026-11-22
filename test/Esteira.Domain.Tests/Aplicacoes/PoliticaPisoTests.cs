using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Domain.Tests.Aplicacoes
{
    // A1.3, critérios 1 (políticas padrão), 11 (limite negativo) e 11a (piso fixo) — a regra mora na entidade.
    [Trait("Fatia", "A1")]
    public class PoliticaPisoTests
    {
        [Theory, DisplayName("Deve criar a política padrão: review avisa; qa, hml e producao bloqueiam com Crítica=0 e Alta=0")]
        [InlineData(AmbientePolitica.Review, ModoPolitica.Avisa, null, null)]
        [InlineData(AmbientePolitica.Qa, ModoPolitica.Bloqueia, 0, 0)]
        [InlineData(AmbientePolitica.Hml, ModoPolitica.Bloqueia, 0, 0)]
        [InlineData(AmbientePolitica.Producao, ModoPolitica.Bloqueia, 0, 0)]
        public void Padrao(AmbientePolitica ambiente, ModoPolitica modo, int? critica, int? alta)
        {
            var p = PoliticaSeveridadeEntity.Padrao(7, ambiente);

            p.AplicacaoId.Should().Be(7);
            p.Ambiente.Should().Be(ambiente);
            p.Modo.Should().Be(modo);
            (p.Critica, p.Alta, p.Media, p.Baixa).Should().Be((critica, alta, (int?)null, (int?)null));
        }

        [Fact, DisplayName("Deve recusar limite negativo ao alterar (politica.limite-negativo)")]
        public void Alterar_LimiteNegativo()
        {
            var p = PoliticaSeveridadeEntity.Padrao(1, AmbientePolitica.Review);

            var r = p.Alterar(null, null, -1, null, ModoPolitica.Avisa);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.limite-negativo");
            p.Media.Should().BeNull();
        }

        [Fact, DisplayName("Deve preferir limite-negativo a abaixo-do-minimo quando os dois valem")]
        public void Alterar_NegativoVenceOPiso()
        {
            var p = PoliticaSeveridadeEntity.Padrao(1, AmbientePolitica.Qa);

            var r = p.Alterar(-1, 0, null, null, ModoPolitica.Avisa);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.limite-negativo");
        }

        [Theory, DisplayName("Deve recusar afrouxar o piso em qa, hml e producao (politica.abaixo-do-minimo)")]
        [InlineData(AmbientePolitica.Qa, 1, 0, ModoPolitica.Bloqueia)]
        [InlineData(AmbientePolitica.Hml, 0, 3, ModoPolitica.Bloqueia)]
        [InlineData(AmbientePolitica.Producao, null, 0, ModoPolitica.Bloqueia)]
        [InlineData(AmbientePolitica.Producao, 0, null, ModoPolitica.Bloqueia)]
        [InlineData(AmbientePolitica.Qa, 0, 0, ModoPolitica.Avisa)]
        public void Alterar_AbaixoDoMinimo(AmbientePolitica ambiente, int? critica, int? alta, ModoPolitica modo)
        {
            var p = PoliticaSeveridadeEntity.Padrao(1, ambiente);

            var r = p.Alterar(critica, alta, null, null, modo);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.abaixo-do-minimo");
            (p.Critica, p.Alta, p.Modo).Should().Be((0, 0, ModoPolitica.Bloqueia));
        }

        [Fact, DisplayName("Deve permitir endurecer: review bloqueando e Média/Baixa com limite em producao")]
        public void Alterar_Endurecer()
        {
            var review = PoliticaSeveridadeEntity.Padrao(1, AmbientePolitica.Review);
            var producao = PoliticaSeveridadeEntity.Padrao(1, AmbientePolitica.Producao);

            review.Alterar(0, 0, null, null, ModoPolitica.Bloqueia).IsSuccess.Should().BeTrue();
            producao.Alterar(0, 0, 5, 10, ModoPolitica.Bloqueia).IsSuccess.Should().BeTrue();

            review.Modo.Should().Be(ModoPolitica.Bloqueia);
            (producao.Media, producao.Baixa).Should().Be((5, 10));
        }

        [Fact, DisplayName("Deve permitir review sem piso: avisa, com Crítica e Alta sem limite")]
        public void Alterar_ReviewSemPiso()
        {
            var review = PoliticaSeveridadeEntity.Padrao(1, AmbientePolitica.Review);

            review.Alterar(null, 4, null, null, ModoPolitica.Avisa).IsSuccess.Should().BeTrue();
            review.Alta.Should().Be(4);
        }
    }
}
