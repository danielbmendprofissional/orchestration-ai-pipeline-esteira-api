using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Escrita da Aplicação e das suas Políticas (EsteiraContext).
    public interface IAplicacaoRepositorio
    {
        Task<AplicacaoEntity?> ObterAsync(long id, CancellationToken cancellationToken = default);

        // `exceto`: Id da própria Aplicação ao alterar.
        Task<bool> SiglaExisteAsync(string sigla, long? exceto, CancellationToken cancellationToken = default);

        Task<bool> GrupoVinculadoAsync(long grupoGitLabId, CancellationToken cancellationToken = default);

        // Depois de perder uma corrida de gravação (X3): descarta o que está pendente no contexto (inclusive a auditoria)
        // e lê do banco o teto que a outra requisição gravou.
        Task<decimal?> ObterTetoCustoGravadoAsync(long id, CancellationToken cancellationToken = default);

        Task AdicionarAsync(AplicacaoEntity aplicacao, CancellationToken cancellationToken = default);

        Task AdicionarPoliticaAsync(PoliticaSeveridadeEntity politica, CancellationToken cancellationToken = default);

        Task<PoliticaSeveridadeEntity?> ObterPoliticaAsync(long aplicacaoId, AmbientePolitica ambiente, CancellationToken cancellationToken = default);
    }
}
