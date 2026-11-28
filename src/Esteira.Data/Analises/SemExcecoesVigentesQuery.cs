using Esteira.Application.Analises;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Data.Analises
{
    // E2: ainda nao existe Excecao (E2b). Nada cobre Achado: critica ou alta bloqueia sempre.
    public sealed class SemExcecoesVigentesQuery : IExcecoesVigentesQuery
    {
        public Task<IReadOnlySet<string>> ObterChavesCobertasAsync(long aplicacaoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
