using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Aplicacoes
{
    // Serviço de domínio: valida a nova lista de dependências de um Projeto contra o grafo da Aplicação (critério 15).
    public static class GrafoDeDependencias
    {
        public static readonly Erro DependenciaInvalida = new(
            "projeto.dependencia-invalida",
            "Dependência inválida: use Projetos da mesma Aplicação, nunca o próprio, e sem formar ciclo.",
            TipoErro.Validation);

        // `dependeDe`: a nova lista (substitui a atual). `projetosDaAplicacao`: Ids de todos os Projetos da Aplicação.
        // `arestas`: dependências já gravadas na Aplicação (ProjetoId depende de DependeDeProjetoId); as do próprio
        // projeto são ignoradas porque a lista nova as substitui.
        public static Result Validar(
            long projetoId,
            IReadOnlyCollection<long> dependeDe,
            IReadOnlyCollection<long> projetosDaAplicacao,
            IEnumerable<(long ProjetoId, long DependeDeProjetoId)> arestas)
        {
            var daAplicacao = projetosDaAplicacao.ToHashSet();
            var alvos = dependeDe.Distinct().ToList();

            if (alvos.Any(a => a == projetoId || !daAplicacao.Contains(a)))
                return Result.Falha(DependenciaInvalida);

            var adjacencia = arestas
                .Where(a => a.ProjetoId != projetoId)
                .GroupBy(a => a.ProjetoId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.DependeDeProjetoId).ToList());

            // Ciclo: algum alvo alcança o próprio Projeto seguindo as dependências.
            var visitados = new HashSet<long>();
            var pilha = new Stack<long>(alvos);
            while (pilha.Count > 0)
            {
                var atual = pilha.Pop();
                if (atual == projetoId)
                    return Result.Falha(DependenciaInvalida);
                if (!visitados.Add(atual))
                    continue;
                if (adjacencia.TryGetValue(atual, out var proximos))
                    foreach (var p in proximos)
                        pilha.Push(p);
            }

            return Result.Sucesso();
        }
    }
}
