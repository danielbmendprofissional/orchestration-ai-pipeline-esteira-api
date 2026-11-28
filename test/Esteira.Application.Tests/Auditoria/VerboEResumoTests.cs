using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Auditoria;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Tests.Auditoria
{
    // X1.3, critério 1 (verbo padrão) e resumo dos campos alterados da lista.
    [Trait("Fatia", "X1")]
    public class VerboEResumoTests
    {
        [Theory, DisplayName("Verbo nulo: a API monta '<criou|alterou|removeu> <entidade legível>'")]
        [InlineData(AcaoAuditoria.Criou, "Projeto", "criou o Projeto")]
        [InlineData(AcaoAuditoria.Alterou, "PoliticaSeveridade", "alterou a Política de severidade")]
        [InlineData(AcaoAuditoria.Removeu, "Destino", "removeu o Destino")]
        [InlineData(AcaoAuditoria.Alterou, "EntidadeNova", "alterou EntidadeNova")]
        public void Padrao(AcaoAuditoria acao, string entidade, string esperado)
            => VerboPadrao.Montar(null, acao, entidade).Should().Be(esperado);

        [Fact, DisplayName("Verbo gravado prevalece sobre o padrão")]
        public void Gravado_Prevalece()
            => VerboPadrao.Montar("aprovou a Fatia V4", AcaoAuditoria.Criou, "AcaoBot").Should().Be("aprovou a Fatia V4");

        [Fact, DisplayName("Resumo: Alterou lista só os campos que mudaram, aninhados com ponto")]
        public void Resumo_Alterou()
            => ResumoDeMudancas.Montar(AcaoAuditoria.Alterou,
                """{"nome":"A","destino":{"replicas":1,"url":"x"}}""",
                """{"nome":"A","destino":{"replicas":2,"url":"x"}}""").Should().Be("destino.replicas");

        [Fact, DisplayName("Resumo: Criou usa os campos do depois e Removeu os do antes")]
        public void Resumo_CriouERemoveu()
        {
            ResumoDeMudancas.Montar(AcaoAuditoria.Criou, null, """{"a":1,"b":2}""").Should().Be("a, b");
            ResumoDeMudancas.Montar(AcaoAuditoria.Removeu, """{"c":1}""", null).Should().Be("c");
        }

        [Fact, DisplayName("Resumo: mais de três campos viram 'e mais N'; JSON ausente ou inválido = vazio")]
        public void Resumo_Longo()
        {
            ResumoDeMudancas.Montar(AcaoAuditoria.Criou, null, """{"a":1,"b":2,"c":3,"d":4,"e":5}""").Should().Be("a, b, c e mais 2");
            ResumoDeMudancas.Montar(AcaoAuditoria.Criou, null, null).Should().BeEmpty();
            ResumoDeMudancas.Montar(AcaoAuditoria.Criou, null, "não é json").Should().BeEmpty();
        }
    }
}
