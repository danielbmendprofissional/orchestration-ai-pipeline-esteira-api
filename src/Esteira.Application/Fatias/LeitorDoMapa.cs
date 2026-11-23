using System.Text.RegularExpressions;

namespace Esteira.Application.Fatias
{
    // Padrão do código da Fatia: B2, E7b, F0.5, B2-L (limpeza, ADR-0025).
    internal static class PadroesDeFatia
    {
        public const string Codigo = @"[A-Z]{1,3}\d+[a-z]?(?:\.\d+)?(?:-L)?";
        public static readonly TimeSpan Limite = TimeSpan.FromSeconds(1);
    }

    public static class LeitorDoMapa
    {
        private static readonly Regex CodigoRegex = new($"^{PadroesDeFatia.Codigo}$", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Link = new(@"\[[^\]]*\]\(([^)\s]+)\)", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Mae = new(@"(?:·\s*)?#(\d+)\s*$", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Separador = new(@"^:?-+:?$", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Celulas = new(@"(?<!\\)\|", RegexOptions.None, PadroesDeFatia.Limite);

        // Uma linha por Fatia, na ordem do mapa. Linha desconhecida é ignorada; nunca lança.
        public static IReadOnlyList<LinhaDoMapa> Ler(string? texto)
        {
            var resultado = new List<LinhaDoMapa>();
            if (string.IsNullOrWhiteSpace(texto)) return resultado;

            try
            {
                foreach (var linha in LinhasDaSecao(texto))
                {
                    var l = LerLinha(linha);
                    if (l is not null) resultado.Add(l);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // formato absurdo: devolve o que já leu
            }
            return resultado;
        }

        private static IEnumerable<string> LinhasDaSecao(string texto)
        {
            var linhas = texto.Replace("\r", "").Split('\n');
            var inicio = Array.FindIndex(linhas, l => l.StartsWith("## Mapa de fatias", StringComparison.OrdinalIgnoreCase));
            if (inicio < 0) return linhas;

            var fim = Array.FindIndex(linhas, inicio + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
            return linhas.Skip(inicio + 1).Take(fim < 0 ? int.MaxValue : fim - inicio - 1);
        }

        private static LinhaDoMapa? LerLinha(string linha)
        {
            var t = linha.Trim();
            if (!t.StartsWith('|')) return null;

            var celulas = Celulas.Split(t.TrimStart('|')).Select(c => c.Trim()).ToList();
            if (celulas.Count > 0 && celulas[^1].Length == 0) celulas.RemoveAt(celulas.Count - 1);
            if (celulas.Count < 3 || celulas.All(c => Separador.IsMatch(c))) return null;

            var codigo = celulas[1];
            if (!CodigoRegex.IsMatch(codigo)) return null;

            var status = celulas[0];
            var feito = status.Equals("feito", StringComparison.OrdinalIgnoreCase);
            var reconhecido = feito || status is "—" or "-" or "";

            var (titulo, caminho, mae) = LerCelulaFatia(celulas[2]);
            var repositorioTexto = celulas.Count > 3 ? celulas[3] : "";

            return new LinhaDoMapa(codigo, titulo, status, reconhecido, feito, caminho, mae,
                LerRepositorios(repositorioTexto), repositorioTexto, FormatoAnterior: celulas.Count < 4);
        }

        private static (string Titulo, string? Caminho, long? Mae) LerCelulaFatia(string celula)
        {
            long? mae = null;
            var m = Mae.Match(celula);
            if (m.Success && long.TryParse(m.Groups[1].Value, out var n))
            {
                mae = n;
                celula = celula[..m.Index].TrimEnd();
            }

            string? caminho = null;
            var link = Link.Match(celula);
            if (link.Success)
            {
                caminho = NormalizarCaminho(link.Groups[1].Value);
                celula = celula[..link.Index];
            }

            var titulo = celula.Trim().TrimEnd('—', '–', '-', '·').Trim();
            return (titulo, caminho, mae);
        }

        private static string? NormalizarCaminho(string caminho)
        {
            caminho = caminho.Trim().Replace('\\', '/');
            while (caminho.StartsWith("./", StringComparison.Ordinal)) caminho = caminho[2..];
            if (caminho.Length == 0 || caminho.Contains("://") || caminho.StartsWith('/') || caminho.Split('/').Contains("..")) return null;
            return caminho.StartsWith("specs/", StringComparison.Ordinal) ? caminho : "specs/" + caminho;
        }

        private static IReadOnlyList<RepositorioFatia> LerRepositorios(string texto)
        {
            var lista = new List<RepositorioFatia>();
            foreach (var parte in texto.Split('+', StringSplitOptions.TrimEntries))
            {
                RepositorioFatia? r = parte.ToLowerInvariant() switch
                {
                    "api" => RepositorioFatia.Api,
                    "frontend" => RepositorioFatia.Frontend,
                    _ => null,
                };
                if (r is { } v && !lista.Contains(v)) lista.Add(v);
            }
            return lista;
        }
    }
}
