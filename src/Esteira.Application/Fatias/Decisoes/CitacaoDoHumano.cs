using System.Text;

namespace Esteira.Application.Fatias.Decisoes
{
    // P2b, critério 4 (spec 03-acoes-do-bot-na-mr.md): o texto que o humano escreveu no Reprovar vai para a MR em
    // citação, cada linha com "> ", para que uma linha como `/merge` ou `/close` não vire ação rápida do GitLab
    // executada como o bot. O nome do humano também é neutralizado (sem quebra de linha e dentro da citação).
    public static class CitacaoDoHumano
    {
        public static string Citar(string nomeDoHumano, string texto)
        {
            // Neutraliza o nome: uma quebra de linha no nome não pode abrir uma linha solta, fora da citação.
            var nome = SemQuebras(nomeDoHumano);

            var corpo = new StringBuilder();
            corpo.Append("> Comentário de ").Append(nome).Append(':');
            corpo.Append('\n').Append('>');

            // Cada linha do texto (inclusive as em branco) fica citada, então nenhuma linha começa com uma quick action.
            foreach (var linha in SemQuebrasDeTexto(texto))
                corpo.Append('\n').Append("> ").Append(linha);

            return corpo.ToString();
        }

        private static string SemQuebras(string? valor)
            => (valor ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

        private static string[] SemQuebrasDeTexto(string? valor)
            => (valor ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
    }
}
