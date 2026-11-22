using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    // Cada limite `null` = sem limite para aquela severidade (A1, critério 11).
    public sealed record LimitesSeveridadeValueObject
    {
        private LimitesSeveridadeValueObject() { }

        private LimitesSeveridadeValueObject(int? critica, int? alta, int? media, int? baixa)
        {
            VulnerabilidadeCritica = critica;
            VulnerabilidadeAlta = alta;
            VulnerabilidadeMedia = media;
            VulnerabilidadeBaixa = baixa;
        }

        public static Result<LimitesSeveridadeValueObject> Criar(int? critica, int? alta, int? media, int? baixa)
            => critica < 0 || alta < 0 || media < 0 || baixa < 0
                ? new Erro("politica.limite-negativo", "Os limites de vulnerabilidades não podem ser negativos.", TipoErro.Validation)
                : Result<LimitesSeveridadeValueObject>.Sucesso(new(critica, alta, media, baixa));

        public int? VulnerabilidadeCritica { get; }
        public int? VulnerabilidadeAlta { get; }
        public int? VulnerabilidadeMedia { get; }
        public int? VulnerabilidadeBaixa { get; }

        // Limite 0 em tudo (qualquer vulnerabilidade excede). Não confundir com null = sem limite.
        public static LimitesSeveridadeValueObject Nenhum => new(0, 0, 0, 0);

        public IReadOnlyList<string> ObterExcessos(ResumoSeveridadeValueObject resumo)
        {
            Guard.AgainstNull(resumo, nameof(resumo));

            var erros = new List<string>();

            if (VulnerabilidadeCritica is int critica && resumo.VulnerabilidadeCritica > critica)
                erros.Add($"Vulnerabilidades críticas excederam o limite. Atual: {resumo.VulnerabilidadeCritica}, Limite: {critica}");

            if (VulnerabilidadeAlta is int alta && resumo.VulnerabilidadeAlta > alta)
                erros.Add($"Vulnerabilidades altas excederam o limite. Atual: {resumo.VulnerabilidadeAlta}, Limite: {alta}");

            if (VulnerabilidadeMedia is int media && resumo.VulnerabilidadeMedia > media)
                erros.Add($"Vulnerabilidades médias excederam o limite. Atual: {resumo.VulnerabilidadeMedia}, Limite: {media}");

            if (VulnerabilidadeBaixa is int baixa && resumo.VulnerabilidadeBaixa > baixa)
                erros.Add($"Vulnerabilidades baixas excederam o limite. Atual: {resumo.VulnerabilidadeBaixa}, Limite: {baixa}");

            return erros;
        }
    }
}
