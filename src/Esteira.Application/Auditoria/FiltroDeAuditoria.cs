using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Auditoria
{
    public sealed record FiltroDeAuditoria(
        Guid? AplicacaoPublicId, string? Quem, CategoriaAuditoria? Categoria, int? PeriodoDias, string? Objeto, string? Busca)
    {
        public const string QuemBot = "@bot";
        public const string QuemPipeline = "@pipeline";

        private static readonly int[] Periodos = [7, 30, 90];

        public FiltroDeAuditoria SemCategoria() => this with { Categoria = null };

        public static Result<FiltroDeAuditoria> Criar(
            Guid? aplicacaoPublicId, string? quem, string? categoria, int? periodoDias, string? objeto, string? busca)
        {
            CategoriaAuditoria? cat = null;
            if (!string.IsNullOrWhiteSpace(categoria))
            {
                cat = CategoriasDeAuditoria.Ler(categoria.Trim());
                if (cat is null)
                    return new Erro("auditoria.categoria-invalida", $"A categoria '{categoria}' não existe.", TipoErro.Validation);
            }

            if (periodoDias is not null && !Periodos.Contains(periodoDias.Value))
                return new Erro("auditoria.periodo-invalido", "O período (periodoDias) deve ser 7, 30 ou 90.", TipoErro.Validation);

            return Result<FiltroDeAuditoria>.Sucesso(new FiltroDeAuditoria(
                aplicacaoPublicId, Limpar(quem), cat, periodoDias, Limpar(objeto), Limpar(busca)));
        }

        private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    public static class CategoriasDeAuditoria
    {
        public static string Texto(CategoriaAuditoria categoria) => Kebab(categoria.ToString());

        public static string Texto(OrigemAuditoria origem) => Kebab(origem.ToString());

        public static CategoriaAuditoria? Ler(string texto)
            => Enum.GetValues<CategoriaAuditoria>().Select(c => (CategoriaAuditoria?)c).FirstOrDefault(c => Texto(c!.Value) == texto);

        private static string Kebab(string nome)
            => string.Concat(nome.Select((c, i) => char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
