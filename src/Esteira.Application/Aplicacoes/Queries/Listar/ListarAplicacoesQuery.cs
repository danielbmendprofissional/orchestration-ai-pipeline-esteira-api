using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.Listar
{
    public sealed record ListarAplicacoesQuery(PaginacaoDto Paginacao, string? Busca, bool? Ativa)
        : IRequest<Result<PaginaDto<AplicacaoListaDto>>>;

    public sealed class ListarAplicacoesHandler(IAplicacaoQuery query, IUsuarioAtual usuario)
        : IRequestHandler<ListarAplicacoesQuery, Result<PaginaDto<AplicacaoListaDto>>>
    {
        public Task<Result<PaginaDto<AplicacaoListaDto>>> Handle(ListarAplicacoesQuery request, CancellationToken cancellationToken)
            => query.ListarAsync(request.Paginacao, request.Busca, request.Ativa, usuario.Id, usuario.EhMor, cancellationToken);
    }
}
