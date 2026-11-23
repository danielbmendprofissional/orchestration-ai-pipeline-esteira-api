using System.Text.RegularExpressions;

namespace Esteira.Application.Fatias
{
    // Links de navegação montados só de ids e caminhos que a Esteira já tem, sobre `GitLab:Url`.
    // Nunca usa URL vinda do GitLab. Entrada fora do formato esperado = null (sem link), nunca exceção.
    public sealed partial class LinksDoGitLab
    {
        private readonly string? _base;

        public LinksDoGitLab(string? baseUrl)
        {
            _base = Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/')
                : null;
        }

        // Segmentos de caminho simples; sem `..`, sem barra dupla, sem query/fragmento.
        [GeneratedRegex(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
        private static partial Regex CaminhoSeguro();

        [GeneratedRegex(@"^[0-9a-fA-F]{4,40}$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
        private static partial Regex ShaSeguro();

        public string? Spec(string? workspace, string? caminhoDaSpec)
            => Montar(workspace, "blob/main", caminhoDaSpec);

        public string? Issue(string? workspace, long numero)
            => numero > 0 ? Montar(workspace, "issues", numero.ToString()) : null;

        public string? Branch(string? projeto, string? branch)
            => Montar(projeto, "tree", branch);

        public string? Mr(string? projeto, long iid)
            => iid > 0 ? Montar(projeto, "merge_requests", iid.ToString()) : null;

        public string? Commit(string? projeto, string? sha)
            => sha is not null && ShaSeguro().IsMatch(sha) ? Montar(projeto, "commit", sha) : null;

        private string? Montar(string? repositorio, string tipo, string? alvo)
        {
            if (_base is null || !Valido(repositorio) || !Valido(alvo))
                return null;

            return $"{_base}/{repositorio}/-/{tipo}/{alvo}";
        }

        private static bool Valido(string? caminho)
        {
            if (string.IsNullOrEmpty(caminho) || caminho.Contains("..", StringComparison.Ordinal))
                return false;

            try
            {
                return CaminhoSeguro().IsMatch(caminho);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
