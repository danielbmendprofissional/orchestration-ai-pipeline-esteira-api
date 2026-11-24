using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Fatias;

namespace Esteira.Domain.Tests.Fatias
{
    // P2b.1 (spec 03-acoes-do-bot-na-mr.md, "Dados"): a Ação guarda a pipeline criada e a Ação falhada que ela repete.
    [Trait("Fatia", "P2b")]
    public class AcaoBotPipelineTests
    {
        private static readonly DateTime T0 = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

        private static AcaoBotEntity Nova() => AcaoBotEntity.Criar(7, "p2", 11, 42, TipoAcaoBot.AbrirReview, 1, null, 5, null, T0).Value;

        [Fact, DisplayName("Deve nascer sem PipelineId e sem ReexecucaoDeAcaoId")]
        public void Criar_SemPipelineNemReexecucao()
        {
            var a = Nova();

            a.PipelineId.Should().BeNull();
            a.ReexecucaoDeAcaoId.Should().BeNull();
        }

        [Fact, DisplayName("Deve gravar o PipelineId quando o GitLab aceita criar a pipeline")]
        public void RegistrarPipeline_Grava()
        {
            var a = Nova();

            a.RegistrarPipeline(1234);

            a.PipelineId.Should().Be(1234);
        }

        [Fact, DisplayName("ReexecucaoDeAcaoId vem na criação quando a Ação repete uma que falhou")]
        public void Criar_ComReexecucao()
        {
            var a = AcaoBotEntity.Criar(7, "p2", 11, 42, TipoAcaoBot.Aprovar, 2, null, 5, null, T0, reexecucaoDeAcaoId: 99).Value;

            a.ReexecucaoDeAcaoId.Should().Be(99);
        }

        [Fact, DisplayName("RegistrarPipeline com id inválido é erro de programação")]
        public void RegistrarPipeline_Invalido()
        {
            var a = Nova();

            var acao = () => a.RegistrarPipeline(0);

            acao.Should().Throw<ArgumentOutOfRangeException>();
            a.PipelineId.Should().BeNull();
        }
    }
}
