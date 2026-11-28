using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Analises
{
    // O que o gate decidiu na hora (a Politica pode mudar depois). Unica por Fluxo + Ambiente + Momento + pipeline:
    // o reenvio sobrescreve (E2).
    public sealed class DecisaoDoGateEntity : EntityBase
    {
        private DecisaoDoGateEntity() { }

        public long FluxoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public MomentoDoGate Momento { get; private set; }
        public DecisaoGate Decisao { get; private set; }
        // Contagem depois das Excecoes.
        public int Critica { get; private set; }
        public int Alta { get; private set; }
        public int Media { get; private set; }
        public int Baixa { get; private set; }
        public string? Motivo { get; private set; }
        public long PipelineGitLabId { get; private set; }
        public DateTime DecididaEm { get; private set; }

        public static DecisaoDoGateEntity Criar(
            long fluxoId, AmbienteDeploy ambiente, MomentoDoGate momento, AvaliacaoDoGate avaliacao, long pipelineGitLabId, DateTime em)
        {
            var decisao = new DecisaoDoGateEntity
            {
                FluxoId = fluxoId,
                Ambiente = ambiente,
                Momento = momento,
                PipelineGitLabId = pipelineGitLabId
            };
            decisao.Registrar(avaliacao, em);
            return decisao;
        }

        // Reenvio do mesmo job: sobrescreve o que foi decidido.
        public void Sobrescrever(AvaliacaoDoGate avaliacao, DateTime em) => Registrar(avaliacao, em);

        private void Registrar(AvaliacaoDoGate avaliacao, DateTime em)
        {
            Guard.AgainstNull(avaliacao, nameof(avaliacao));
            Guard.AgainstInvalidEnum<AmbienteDeploy>(Ambiente, nameof(Ambiente));
            Guard.AgainstInvalidEnum<MomentoDoGate>(Momento, nameof(Momento));

            Decisao = avaliacao.Decisao;
            Critica = avaliacao.Contagem.Critica;
            Alta = avaliacao.Contagem.Alta;
            Media = avaliacao.Contagem.Media;
            Baixa = avaliacao.Contagem.Baixa;
            Motivo = avaliacao.Motivo;
            DecididaEm = em;
        }
    }
}
