using System.Text.RegularExpressions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Analises
{
    // Resultado de uma ferramenta ou agente sobre um Artefato ou Deploy, com seus Achados (E2, spec 03-analises-e-gate.md).
    // O reenvio do mesmo job (indice unico no banco) substitui a Analise e os Achados dela.
    public sealed partial class AnaliseEntity : AggregateRoot
    {
        public const int MaxAchados = 2000, MaxDestino = 100;

        private readonly List<AchadoEntity> _achados = [];

        private AnaliseEntity() { }

        public long FluxoId { get; private set; }
        public AmbienteDeploy Ambiente { get; private set; }
        public EscopoAnalise Escopo { get; private set; }
        // So no escopo deploy. Nulo no banco vira '' no indice unico.
        public string? DestinoNome { get; private set; }
        public string Fonte { get; private set; } = string.Empty;
        public TipoAnalise Tipo { get; private set; }
        public VereditoAnalise Veredito { get; private set; }
        // Informativo: a Esteira guarda e mostra, nunca recusa nem bloqueia por ele (o teto e do runner, ADR-0012).
        public decimal? CustoUsd { get; private set; }
        public long PipelineGitLabId { get; private set; }
        public DateTime RecebidaEm { get; private set; }
        public IReadOnlyList<AchadoEntity> Achados => _achados;

        public static Result<AnaliseEntity> Criar(
            long fluxoId, AmbienteDeploy ambiente, EscopoAnalise escopo, string? destinoNome, string fonte, TipoAnalise tipo,
            VereditoAnalise veredito, decimal? custoUsd, long pipelineGitLabId, DateTime recebidaEm, IReadOnlyList<NovoAchado> achados)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));
            Guard.AgainstInvalidEnum<EscopoAnalise>(escopo, nameof(escopo));
            Guard.AgainstInvalidEnum<TipoAnalise>(tipo, nameof(tipo));
            Guard.AgainstInvalidEnum<VereditoAnalise>(veredito, nameof(veredito));
            Guard.AgainstNull(achados, nameof(achados));

            if (fonte is null || !FonteValida().IsMatch(fonte))
                return new Erro("analise.fonte-invalida", "A fonte deve ter de 1 a 64 caracteres entre a-z, 0-9 e hífen.", TipoErro.Validation);

            if (destinoNome is not null && (escopo != EscopoAnalise.Deploy || destinoNome.Length == 0 || destinoNome.Length > MaxDestino))
                return new Erro("analise.destino-invalido", "O destino só vale no escopo deploy e tem até 100 caracteres.", TipoErro.Validation);

            if (custoUsd < 0)
                return new Erro("analise.custo-invalido", "O custo_usd não pode ser negativo.", TipoErro.Validation);

            if (achados.Count > MaxAchados)
                return new Erro("analise.achados-demais", "Uma Análise aceita até 2000 Achados.", TipoErro.Validation);

            var analise = new AnaliseEntity
            {
                FluxoId = fluxoId,
                Ambiente = ambiente,
                Escopo = escopo,
                DestinoNome = destinoNome,
                Fonte = fonte,
                Tipo = tipo,
                Veredito = veredito,
                CustoUsd = custoUsd,
                PipelineGitLabId = pipelineGitLabId,
                RecebidaEm = recebidaEm
            };

            foreach (var novo in achados)
            {
                var achado = AchadoEntity.Criar(fonte, novo);
                if (!achado.IsSuccess)
                    return Result<AnaliseEntity>.Falha(achado.Errors.ToArray());
                analise._achados.Add(achado.Value);
            }

            return Result<AnaliseEntity>.Sucesso(analise);
        }

        [GeneratedRegex(@"\A[a-z0-9-]{1,64}\z")]
        private static partial Regex FonteValida();
    }
}
