using System.Text.RegularExpressions;
using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Fatias
{
    public enum EstadoDoHotfix { AguardandoCorrecao, MrAberta, ProntoParaEntrega }

    public sealed record MrDoHotfix(RepositorioFatia Repositorio, long Iid, string Titulo, string Estado, string Branch);

    // `Mr` = a MR que define o estado (aberta ou mergeada); nula em Aguardando a correção.
    public sealed record HotfixLido(long Iid, string Motivo, EstadoDoHotfix Estado, MrDoHotfix? Mr);

    // Pedido de hotfix (ADR-0027): issue `Hotfix: <motivo>` com label `ready-for-human`. Funções puras; nunca lançam.
    public static class LeitorDeHotfix
    {
        public const string Label = "ready-for-human";
        public const string PrefixoDaBranch = "hotfix/";
        public const int TamanhoMaximo = 200;

        private static readonly Regex Titulo = new(@"^\s*Hotfix:\s*(?<m>\S.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline, PadroesDeFatia.Limite);

        // null = o título não é de pedido de hotfix. Motivo cortado em 200 caracteres.
        public static string? Motivo(string? titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return null;
            try
            {
                var m = Titulo.Match(titulo);
                if (!m.Success) return null;
                var motivo = m.Groups["m"].Value;
                return motivo.Length > TamanhoMaximo ? motivo[..TamanhoMaximo] : motivo;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        // A MR cita a issue por `#N` (com ou sem caminho do projeto na frente) ou pelo link `/issues/N`, no título ou na descrição.
        public static bool Cita(MergeRequestGitLabDto mr, long issue)
        {
            var texto = $"{mr.Titulo}\n{mr.Descricao}";
            try
            {
                return Regex.IsMatch(texto, $@"(?:#|/issues/){issue}(?!\d)", RegexOptions.None, PadroesDeFatia.Limite);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        public static bool EhDeHotfix(MergeRequestGitLabDto mr)
            => mr.BranchOrigem?.StartsWith(PrefixoDaBranch, StringComparison.Ordinal) == true;
    }

    public static class DerivadorDeHotfix
    {
        // Mais novo primeiro. MR mergeada > aberta; fechada sem merge não conta (volta a Aguardando a correção).
        public static IReadOnlyList<HotfixLido> Derivar(
            IEnumerable<IssueGitLabDto> issues, IEnumerable<(RepositorioFatia Repositorio, MergeRequestGitLabDto Mr)> mrs)
        {
            var candidatas = mrs.Where(x => LeitorDeHotfix.EhDeHotfix(x.Mr)).ToList();
            var lidos = new List<HotfixLido>();

            foreach (var issue in issues.OrderByDescending(i => i.Iid))
            {
                if (!issue.Labels.Contains(LeitorDeHotfix.Label)) continue;
                var motivo = LeitorDeHotfix.Motivo(issue.Titulo);
                if (motivo is null) continue;

                var citam = candidatas.Where(x => LeitorDeHotfix.Cita(x.Mr, issue.Iid)).ToList();
                var escolhida = Escolher(citam, "merged") ?? Escolher(citam, "opened");

                lidos.Add(escolhida is null
                    ? new HotfixLido(issue.Iid, motivo, EstadoDoHotfix.AguardandoCorrecao, null)
                    : new HotfixLido(
                        issue.Iid, motivo,
                        escolhida.Value.Mr.Estado.Equals("merged", StringComparison.OrdinalIgnoreCase) ? EstadoDoHotfix.ProntoParaEntrega : EstadoDoHotfix.MrAberta,
                        new MrDoHotfix(escolhida.Value.Repositorio, escolhida.Value.Mr.Iid, escolhida.Value.Mr.Titulo, escolhida.Value.Mr.Estado, escolhida.Value.Mr.BranchOrigem)));
            }

            return lidos;
        }

        private static (RepositorioFatia Repositorio, MergeRequestGitLabDto Mr)? Escolher(
            List<(RepositorioFatia Repositorio, MergeRequestGitLabDto Mr)> mrs, string estado)
        {
            var achadas = mrs.Where(x => x.Mr.Estado.Equals(estado, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Mr.Iid).ToList();
            return achadas.Count == 0 ? null : achadas[0];
        }
    }
}
