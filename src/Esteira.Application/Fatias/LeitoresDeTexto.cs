using System.Text.RegularExpressions;

namespace Esteira.Application.Fatias
{
    public static class LeitorDeSpec
    {
        private static readonly Regex Comentario = new(@"<!--.*?-->", RegexOptions.Singleline, PadroesDeFatia.Limite);
        private static readonly Regex TituloRegex = new(@"^#\s+(?:[A-Za-z]{0,3}\d+[a-z]?\s+[—–-]\s+)?(.+?)\s*$", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Campo = new(@"^(risco|flag|base):\s*(.*?)\s*$", RegexOptions.IgnoreCase, PadroesDeFatia.Limite);

        // Cabeçalho = tudo antes da primeira seção `## `. Nunca lança.
        public static CabecalhoDaSpec Ler(string? texto)
        {
            string? titulo = null, risco = null, baseTexto = null;
            var flag = true;
            var flagPresente = false;

            try
            {
                foreach (var bruta in (texto ?? "").Replace("\r", "").Split('\n'))
                {
                    if (bruta.StartsWith("## ", StringComparison.Ordinal)) break;
                    var linha = Comentario.Replace(bruta, "").Trim();

                    if (titulo is null && linha.StartsWith("# ", StringComparison.Ordinal))
                    {
                        var t = TituloRegex.Match(linha);
                        if (t.Success) titulo = t.Groups[1].Value;
                        continue;
                    }

                    var c = Campo.Match(linha);
                    if (!c.Success) continue;
                    var valor = c.Groups[2].Value;
                    if (valor.Length == 0) continue;

                    switch (c.Groups[1].Value.ToLowerInvariant())
                    {
                        case "risco": risco = valor.Split(' ')[0].ToLowerInvariant(); break;
                        case "base": baseTexto = valor; break;
                        case "flag":
                            flagPresente = true;
                            flag = !valor.StartsWith("n", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
            }

            return new CabecalhoDaSpec(titulo, risco, flag, baseTexto, FormatoAnterior: risco is null || baseTexto is null || !flagPresente);
        }
    }

    public static class LeitorDeTitulo
    {
        private static readonly Regex Padrao = new(
            $@"^\s*(?<c>[A-Z]{{1,3}}\d+[a-z]?(?:-L)?)(?:\.(?<n>\d+))?\s+[—–-]\s+(?<t>.+?)\s*$",
            RegexOptions.None, PadroesDeFatia.Limite);

        // `codigosConhecidos` desfaz a ambiguidade de códigos com ponto (F0.5 é fatia, não a task 5 da F0).
        public static TituloDeFatia? Ler(string? titulo, IEnumerable<string>? codigosConhecidos = null)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return null;
            try
            {
                var m = Padrao.Match(titulo);
                if (!m.Success) return null;

                var codigo = m.Groups["c"].Value;
                var titulo2 = m.Groups["t"].Value;
                if (!m.Groups["n"].Success) return new TituloDeFatia(codigo, null, titulo2);

                var n = int.Parse(m.Groups["n"].Value);
                var inteiro = $"{codigo}.{n}";
                if (codigosConhecidos?.Contains(inteiro) == true) return new TituloDeFatia(inteiro, null, titulo2);
                return new TituloDeFatia(codigo, n, titulo2);
            }
            catch (Exception e) when (e is RegexMatchTimeoutException or FormatException or OverflowException)
            {
                return null;
            }
        }
    }

    public static class LeitorDoBlocoDeExecucao
    {
        private const string Abre = "<!-- execucao -->";
        private const string Fecha = "<!-- /execucao -->";

        private static readonly Regex Area = new(@"\*\*Área:\*\*\s*`([^`]*)`", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Modo = new(@"\*\*Modo:\*\*\s*([^·\r\n]+?)\s*(?:·|$)", RegexOptions.Multiline, PadroesDeFatia.Limite);
        private static readonly Regex Inicio = new(@"\*\*Início:\*\*\s*(\S+)", RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Task = new(
            $@"^\s*-\s*\[(?<x>[xX ])\]\s+(?<c>{PadroesDeFatia.Codigo})\.(?<n>\d+)\s+#(?<i>\d+)\s+[—–-]\s*(?<r>.*?)\s*$",
            RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Final = new(@"^\s*-\s*\[(?<x>[xX ])\]\s+(?<k>Suite completa|Review|MR)\s*$", RegexOptions.IgnoreCase, PadroesDeFatia.Limite);
        private static readonly Regex Sha = new(@"^`([0-9a-fA-F]{4,40})`$", RegexOptions.None, PadroesDeFatia.Limite);

        // null = bloco ausente ("ainda não começou"). Linha que não casa é ignorada; marcador sem fechamento vai até o fim.
        public static BlocoDeExecucao? Ler(string? descricao)
        {
            if (string.IsNullOrEmpty(descricao)) return null;
            var inicio = descricao.IndexOf(Abre, StringComparison.Ordinal);
            if (inicio < 0) return null;

            var corpoInicio = inicio + Abre.Length;
            var fim = descricao.IndexOf(Fecha, corpoInicio, StringComparison.Ordinal);
            var corpo = fim < 0 ? descricao[corpoInicio..] : descricao[corpoInicio..fim];

            var tasks = new List<TaskDoBloco>();
            bool suite = false, review = false, mr = false;

            try
            {
                foreach (var linha in corpo.Replace("\r", "").Split('\n'))
                {
                    var t = Task.Match(linha);
                    if (t.Success)
                    {
                        if (int.TryParse(t.Groups["n"].Value, out var n) && long.TryParse(t.Groups["i"].Value, out var i))
                            tasks.Add(LerTask(t.Groups["c"].Value, n, i, t.Groups["x"].Value != " ", t.Groups["r"].Value));
                        continue;
                    }

                    var f = Final.Match(linha);
                    if (!f.Success) continue;
                    var feito = f.Groups["x"].Value != " ";
                    switch (f.Groups["k"].Value.ToLowerInvariant())
                    {
                        case "suite completa": suite = feito; break;
                        case "review": review = feito; break;
                        default: mr = feito; break;
                    }
                }

                return new BlocoDeExecucao(
                    Capturar(Area, corpo), Capturar(Modo, corpo), Capturar(Inicio, corpo), tasks, suite, review, mr);
            }
            catch (RegexMatchTimeoutException)
            {
                return new BlocoDeExecucao(null, null, null, tasks, suite, review, mr);
            }
        }

        private static string? Capturar(Regex regex, string texto)
        {
            var m = regex.Match(texto);
            return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
        }

        private static TaskDoBloco LerTask(string codigo, int numero, long issue, bool feita, string resto)
        {
            string? sha = null, executor = null;
            bool andamento = false, review = false;
            foreach (var parte in resto.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var s = Sha.Match(parte);
                if (s.Success) sha = s.Groups[1].Value;
                else if (parte.Equals("em andamento", StringComparison.OrdinalIgnoreCase)) andamento = true;
                else if (parte.Equals("review PASS", StringComparison.OrdinalIgnoreCase)) review = true;
                else executor ??= parte;
            }
            return new TaskDoBloco(codigo, numero, issue, feita, sha, andamento, executor, review);
        }
    }

    public static class LeitorDeBranch
    {
        private static readonly Regex Padrao = new(
            @"^feature/(?<c>[a-z]{1,3}\d+[a-z]?(?:\.\d+)?(?:-l)?)-(?<s>.+)$", RegexOptions.None, PadroesDeFatia.Limite);

        public static BranchDeFatia? Ler(string? branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return null;
            try
            {
                var m = Padrao.Match(branch.Trim());
                return m.Success ? new BranchDeFatia(m.Groups["c"].Value, m.Groups["s"].Value) : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }
    }

    public static class LeitorDeCommit
    {
        private static readonly Regex Padrao = new(
            $@"^(?<t>[a-z]+)(?:\([^)]*\))?!?:\s+(?<d>.+?)\s+\((?<c>{PadroesDeFatia.Codigo})\.(?<n>\d+)\s+·\s+workspace#(?<i>\d+)\)\s*$",
            RegexOptions.None, PadroesDeFatia.Limite);
        private static readonly Regex Executor = new(@"^Executed-by:\s*(.+?)\s*$", RegexOptions.Multiline, PadroesDeFatia.Limite);

        public static CommitDeTask? Ler(string? mensagem)
        {
            if (string.IsNullOrWhiteSpace(mensagem)) return null;
            try
            {
                var texto = mensagem.Replace("\r", "");
                var primeira = texto.Split('\n')[0];
                var m = Padrao.Match(primeira);
                if (!m.Success || !int.TryParse(m.Groups["n"].Value, out var n) || !long.TryParse(m.Groups["i"].Value, out var i)) return null;

                var e = Executor.Match(texto);
                return new CommitDeTask(m.Groups["t"].Value, m.Groups["d"].Value, m.Groups["c"].Value, n, i, e.Success ? e.Groups[1].Value : null);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }
    }
}
