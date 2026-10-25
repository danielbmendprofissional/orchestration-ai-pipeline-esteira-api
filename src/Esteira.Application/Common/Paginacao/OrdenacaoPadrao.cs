using System.Linq.Expressions;

namespace Esteira.Application.Common.Paginacao
{
    // Ordenação obrigatória de toda lista paginada: vale sozinha sem `sort` e entra como desempate
    // com `sort`, para que a mesma página pedida duas vezes devolva os mesmos itens (ADR-0002).
    public sealed class OrdenacaoPadrao<T>
    {
        private readonly List<Func<IQueryable<T>, IOrderedQueryable<T>?, IOrderedQueryable<T>>> _passos = [];

        private OrdenacaoPadrao() { }

        public static OrdenacaoPadrao<T> Por<TChave>(Expression<Func<T, TChave>> chave, bool descendente = false)
            => new OrdenacaoPadrao<T>().Depois(chave, descendente);

        public OrdenacaoPadrao<T> Depois<TChave>(Expression<Func<T, TChave>> chave, bool descendente = false)
        {
            _passos.Add((query, ordenada) => (ordenada, descendente) switch
            {
                (null, false) => query.OrderBy(chave),
                (null, true) => query.OrderByDescending(chave),
                (_, false) => ordenada.ThenBy(chave),
                (_, true) => ordenada.ThenByDescending(chave)
            });
            return this;
        }

        // `primaria` é a ordenação pedida pelo cliente, quando houver; os critérios padrão vêm depois dela.
        public IOrderedQueryable<T> Aplicar(IQueryable<T> query, IOrderedQueryable<T>? primaria = null)
        {
            var ordenada = primaria;
            foreach (var passo in _passos)
                ordenada = passo(query, ordenada);
            return ordenada!;
        }
    }
}
