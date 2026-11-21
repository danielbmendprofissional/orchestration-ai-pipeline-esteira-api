using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.ListarPapeis
{
    public sealed record ListarPapeisQuery(Guid AplicacaoId) : IRequest<Result<IReadOnlyList<PapelResumoDto>>>;

    public sealed class ListarPapeisHandler(IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao, IPapelQuery query)
        : IRequestHandler<ListarPapeisQuery, Result<IReadOnlyList<PapelResumoDto>>>
    {
        public async Task<Result<IReadOnlyList<PapelResumoDto>>> Handle(ListarPapeisQuery request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.Ver, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<IReadOnlyList<PapelResumoDto>>.Falha(autorizado.Errors.ToArray());

            return Result<IReadOnlyList<PapelResumoDto>>.Sucesso(await query.ListarAsync(id.Value, cancellationToken));
        }
    }
}
