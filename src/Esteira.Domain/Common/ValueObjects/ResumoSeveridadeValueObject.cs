using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record ResumoSeveridadeValueObject
    {
        private ResumoSeveridadeValueObject() { }

        private ResumoSeveridadeValueObject(int critica, int alta, int media, int baixa)
        {
            VulnerabilidadeCritica = critica;
            VulnerabilidadeAlta = alta;
            VulnerabilidadeMedia = media;
            VulnerabilidadeBaixa = baixa;
        }

        public static Result<ResumoSeveridadeValueObject> Criar(int critica, int alta, int media, int baixa)
            => critica < 0 || alta < 0 || media < 0 || baixa < 0
                ? new Erro("severidade.quantidade-negativa", "As quantidades de vulnerabilidades não podem ser negativas.", TipoErro.Validation)
                : Result<ResumoSeveridadeValueObject>.Sucesso(new(critica, alta, media, baixa));

        public int VulnerabilidadeCritica { get; }
        public int VulnerabilidadeAlta { get; }
        public int VulnerabilidadeMedia { get; }
        public int VulnerabilidadeBaixa { get; }

        public int Total => VulnerabilidadeCritica + VulnerabilidadeAlta + VulnerabilidadeMedia + VulnerabilidadeBaixa;

        public static ResumoSeveridadeValueObject Vazio => new(0, 0, 0, 0);

        public ResumoSeveridadeValueObject Somar(ResumoSeveridadeValueObject outro)
        {
            Guard.AgainstNull(outro, nameof(outro));

            return new(
                VulnerabilidadeCritica + outro.VulnerabilidadeCritica,
                VulnerabilidadeAlta + outro.VulnerabilidadeAlta,
                VulnerabilidadeMedia + outro.VulnerabilidadeMedia,
                VulnerabilidadeBaixa + outro.VulnerabilidadeBaixa);
        }

        public static ResumoSeveridadeValueObject SomarTodos(IEnumerable<ResumoSeveridadeValueObject> resumos)
        {
            Guard.AgainstNull(resumos, nameof(resumos));

            return resumos.Aggregate(Vazio, (acumulado, resumo) => acumulado.Somar(resumo));
        }

        public static ResumoSeveridadeValueObject DeSeveridades(IEnumerable<string> severidades)
        {
            Guard.AgainstNull(severidades, nameof(severidades));

            int critica = 0, alta = 0, media = 0, baixa = 0;

            foreach (var severidade in severidades)
            {
                if (severidade is null) continue;

                switch (severidade.Trim().ToUpperInvariant())
                {
                    case "CRITICAL":
                        critica++;
                        break;
                    case "ERROR":
                    case "HIGH":
                        alta++;
                        break;
                    case "WARNING":
                    case "MEDIUM":
                        media++;
                        break;
                    case "INFO":
                    case "LOW":
                    case "UNKNOWN":
                        baixa++;
                        break;
                }
            }

            return new(critica, alta, media, baixa);
        }
    }
}
