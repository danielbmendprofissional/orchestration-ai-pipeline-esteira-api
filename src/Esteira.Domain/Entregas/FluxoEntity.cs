using System.Text.RegularExpressions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Entregas
{
    public sealed partial class FluxoEntity : EntityBase
    {
        private readonly List<FluxoFatiaEntity> _fatias = [];
        private readonly List<EtapaFluxoEntity> _etapas = [];

        private FluxoEntity() { }

        public long EntregaId { get; private set; }
        public long ProjetoId { get; private set; }
        public TipoMudanca TipoMudanca { get; private set; }
        // Nulo até o Artefato chegar (o `esteira-abrir` do `fluxo` registra o primeiro).
        public string? Sha { get; private set; }
        // Nasce na confirmação em produção (E4b); fica nula aqui.
        public string? Versao { get; private set; }
        public IReadOnlyList<FluxoFatiaEntity> Fatias => _fatias;
        // Abertura, QA, HML e PRD (E1b): criadas com o Fluxo.
        public IReadOnlyList<EtapaFluxoEntity> Etapas => _etapas;

        public EtapaFluxoEntity Etapa(EtapaDoFluxo etapa) => _etapas.Single(e => e.Etapa == etapa);

        internal static Result<FluxoEntity> Criar(long projetoId, TipoMudanca tipo, IReadOnlyList<FatiaDaEntrega> fatias, DateTime abertaEm)
        {
            Guard.AgainstInvalidEnum<TipoMudanca>(tipo, nameof(tipo));

            var fluxo = new FluxoEntity { ProjetoId = projetoId, TipoMudanca = tipo };
            foreach (var fatia in fatias)
            {
                var codigo = (fatia.Codigo ?? string.Empty).Trim().ToUpperInvariant();
                if (codigo.Length == 0)
                    return new Erro("entrega.fatia-invalida", "Informe o código da Fatia.", TipoErro.Validation);
                if (fluxo._fatias.Any(f => f.CodigoFatia == codigo))
                    return new Erro("entrega.fatia-duplicada", $"A Fatia {codigo} aparece mais de uma vez no mesmo Projeto.", TipoErro.Validation);

                fluxo._fatias.Add(new FluxoFatiaEntity(codigo, fatia.ComFlag));
            }

            fluxo._etapas.Add(new EtapaFluxoEntity(EtapaDoFluxo.Abertura, EstadoEtapa.Concluido, abertaEm));
            foreach (var etapa in new[] { EtapaDoFluxo.Qa, EtapaDoFluxo.Hml, EtapaDoFluxo.Prd })
                fluxo._etapas.Add(new EtapaFluxoEntity(etapa, EstadoEtapa.NaoIniciado, abertaEm));

            return Result<FluxoEntity>.Sucesso(fluxo);
        }

        // O primeiro SHA registra; o mesmo SHA de novo é idempotente; outro SHA é recusado (o Artefato do Fluxo é um só).
        public Result RegistrarSha(string sha)
        {
            if (sha is null || !ShaValido().IsMatch(sha))
                return Result.Falha(new Erro("pipeline.sha-invalido", "O SHA precisa ter 40 caracteres hexadecimais minúsculos.", TipoErro.Validation));

            if (Sha is null)
            {
                Sha = sha;
                return Result.Sucesso();
            }

            return Sha == sha
                ? Result.Sucesso()
                : Result.Falha(new Erro("pipeline.sha-diferente", "O Fluxo já tem outro SHA registrado.", TipoErro.Conflict));
        }

        // feature_flags do Ambiente (spec E1): qa/hml ligam e produção desliga cada Fatia com flag; Fatia sem flag não aparece.
        // Ordem ordinal pelo código, para a resposta ser estável.
        public string FeatureFlags(AmbienteDeploy ambiente)
        {
            Guard.AgainstInvalidEnum<AmbienteDeploy>(ambiente, nameof(ambiente));

            var valor = ambiente == AmbienteDeploy.Producao ? "false" : "true";
            return string.Join(',', _fatias.Where(f => f.ComFlag)
                .Select(f => f.CodigoFatia).OrderBy(c => c, StringComparer.Ordinal).Select(c => $"{c}={valor}"));
        }

        [GeneratedRegex("^[0-9a-f]{40}$")]
        private static partial Regex ShaValido();
    }

    public sealed class FluxoFatiaEntity : EntityBase
    {
        private FluxoFatiaEntity() { }

        internal FluxoFatiaEntity(string codigoFatia, bool comFlag)
        {
            CodigoFatia = codigoFatia;
            ComFlag = comFlag;
        }

        public long FluxoId { get; private set; }
        // Em maiúsculo, como no mapa (B2).
        public string CodigoFatia { get; private set; } = default!;
        // Espelha `flag: sim|não` da spec no momento da abertura.
        public bool ComFlag { get; private set; }
    }
}
