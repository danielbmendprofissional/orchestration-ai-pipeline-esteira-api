using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;
namespace Esteira.Domain.Common.ValueObjects
{
    public sealed record LimitesSeveridadeValueObject
    {
        private LimitesSeveridadeValueObject() { }

        private LimitesSeveridadeValueObject(int critica, int alta, int media, int baixa)
        {
            VulnerabilidadeCritica = critica;
            VulnerabilidadeAlta = alta;
            VulnerabilidadeMedia = media;
            VulnerabilidadeBaixa = baixa;
        }

        public static Result<LimitesSeveridadeValueObject> Criar(int critica, int alta, int media, int baixa)
            => critica < 0 || alta < 0 || media < 0 || baixa < 0
                ? new Erro("politica.limite-negativo", "Os limites de vulnerabilidades não podem ser negativos.", TipoErro.Validation)
                : Result<LimitesSeveridadeValueObject>.Sucesso(new(critica, alta, media, baixa));

        public int VulnerabilidadeCritica { get; }
        public int VulnerabilidadeAlta { get; }
        public int VulnerabilidadeMedia { get; }
        public int VulnerabilidadeBaixa { get; }

        public static LimitesSeveridadeValueObject Nenhum => new(0, 0, 0, 0);

        public IReadOnlyList<string> ObterExcessos(ResumoSeveridadeValueObject resumo)
        {
            Guard.AgainstNull(resumo, nameof(resumo));

            var erros = new List<string>();

            if (resumo.VulnerabilidadeCritica > VulnerabilidadeCritica)
                erros.Add($"Vulnerabilidades críticas excederam o limite. Atual: {resumo.VulnerabilidadeCritica}, Limite: {VulnerabilidadeCritica}");

            if (resumo.VulnerabilidadeAlta > VulnerabilidadeAlta)
                erros.Add($"Vulnerabilidades altas excederam o limite. Atual: {resumo.VulnerabilidadeAlta}, Limite: {VulnerabilidadeAlta}");

            if (resumo.VulnerabilidadeMedia > VulnerabilidadeMedia)
                erros.Add($"Vulnerabilidades médias excederam o limite. Atual: {resumo.VulnerabilidadeMedia}, Limite: {VulnerabilidadeMedia}");

            if (resumo.VulnerabilidadeBaixa > VulnerabilidadeBaixa)
                erros.Add($"Vulnerabilidades baixas excederam o limite. Atual: {resumo.VulnerabilidadeBaixa}, Limite: {VulnerabilidadeBaixa}");

            return erros;
        }
    }
}
