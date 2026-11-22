using Esteira.Application.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Interfaces.Aplicacoes
{
    public interface IAutorizacaoAplicacao
    {
        Task<AcessoAplicacao> ObterAcessoAsync(long aplicacaoId, CancellationToken cancellationToken = default);

        // Sucesso, NotFound aplicacao.nao-encontrada (sem papel e sem MOR) ou Forbidden acesso.papel-insuficiente.
        Task<Result> AutorizarAsync(long aplicacaoId, OperacaoAplicacao operacao, CancellationToken cancellationToken = default);

        Task<Result<PermissoesAplicacao>> PermissoesAsync(long aplicacaoId, CancellationToken cancellationToken = default);

        // Criar Aplicação não depende de uma Aplicação existente: só o MOR.
        bool PodeCriar();
    }
}
