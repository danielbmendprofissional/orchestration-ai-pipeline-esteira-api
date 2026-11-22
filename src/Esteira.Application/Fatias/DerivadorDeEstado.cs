using Esteira.Application.Interfaces.Integracoes;

namespace Esteira.Application.Fatias
{
    // Estado derivado da Fatia (nunca gravado). Puro: recebe o que já foi lido. Nunca lança.
    public static class DerivadorDeEstado
    {
        public const string EmExecucaoLabel = "em-execucao";

        public static ResultadoDeEstado Derivar(
            LinhaDoMapa linha, IssueGitLabDto? mae, BlocoDeExecucao? bloco, IReadOnlyList<MrDaFatia> mrs)
        {
            var daFatia = (mrs ?? [])
                .Where(m => m?.Mr is not null && DaFatia(m.Mr.Titulo, linha.Codigo))
                .ToList();

            var abertas = daFatia.Where(m => Eh(m.Mr.Estado, "opened")).ToList();
            var requeridos = linha.Repositorios.Count > 0
                ? linha.Repositorios
                : daFatia.Select(m => m.Repositorio).Distinct().ToList();
            bool Mergeado(RepositorioFatia r) => daFatia.Any(m => m.Repositorio == r && Eh(m.Mr.Estado, "merged"));

            if (linha.Feito || (requeridos.Count > 0 && requeridos.All(Mergeado)))
                return new ResultadoDeEstado(EstadoDaFatia.ProntaParaEntrega, null, false, linha.FormatoAnterior);

            var emCorrecao = daFatia
                .Where(m => Eh(m.Mr.Estado, "closed"))
                .Select(m => m.Repositorio)
                .Any(r => !Mergeado(r) && abertas.All(a => a.Repositorio != r));

            if (abertas.Count > 0)
                return new ResultadoDeEstado(EstadoDaFatia.EmRevisao, ProgressoDoCi(abertas), emCorrecao, linha.FormatoAnterior);

            if (emCorrecao)
                return new ResultadoDeEstado(EstadoDaFatia.EmRevisao, "em correção", true, linha.FormatoAnterior);

            if (mae?.Labels?.Any(l => string.Equals(l, EmExecucaoLabel, StringComparison.OrdinalIgnoreCase)) == true)
                return new ResultadoDeEstado(EstadoDaFatia.EmExecucao, bloco?.Progresso, false, linha.FormatoAnterior || bloco is null);

            var estado = mae is not null || linha.CaminhoSpec is not null ? EstadoDaFatia.ComSpec : EstadoDaFatia.Planejada;
            return new ResultadoDeEstado(estado, null, false, linha.FormatoAnterior);
        }

        // A mãe é a issue com o `#n` do mapa; sem ele (ou se não veio na lista), a de título `<código> — …` sem número de task.
        public static IssueGitLabDto? ResolverMae(LinhaDoMapa linha, IReadOnlyList<IssueGitLabDto> issues)
        {
            if (issues is null) return null;
            if (linha.IssueMae is { } n && issues.FirstOrDefault(i => i?.Iid == n) is { } porNumero) return porNumero;

            return issues.FirstOrDefault(i =>
            {
                var t = LeitorDeTitulo.Ler(i?.Titulo, [linha.Codigo]);
                return t is not null && t.NumeroTask is null && string.Equals(t.Codigo, linha.Codigo, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool DaFatia(string? titulo, string codigo)
        {
            var t = LeitorDeTitulo.Ler(titulo, [codigo]);
            return t is not null && t.NumeroTask is null && string.Equals(t.Codigo, codigo, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Eh(string? estado, string esperado) =>
            string.Equals(estado, esperado, StringComparison.OrdinalIgnoreCase);

        // Vermelho > rodando > verde; sem pipeline (ou status que não entendemos) não diz nada.
        private static string? ProgressoDoCi(IReadOnlyList<MrDaFatia> abertas)
        {
            var status = abertas.Select(m => m.Mr.StatusPipeline?.ToLowerInvariant()).ToList();
            if (status.Contains("failed")) return "CI vermelho";
            if (status.Any(s => s is "running" or "pending" or "created" or "preparing" or "scheduled" or "waiting_for_resource"))
                return "CI rodando";
            return status.All(s => s == "success") ? "CI verde, aguardando você" : null;
        }
    }
}
