using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Analises
{
    // Porta de leitura (E2): ChaveDoTipo dos Achados cobertos por Excecao vigente numa Aplicacao e Ambiente.
    // A E2b a implementa de verdade; o gate passa a descontar os cobertos sem mudar este contrato.
    public interface IExcecoesVigentesQuery
    {
        Task<IReadOnlySet<string>> ObterChavesCobertasAsync(long aplicacaoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default);
    }
}
