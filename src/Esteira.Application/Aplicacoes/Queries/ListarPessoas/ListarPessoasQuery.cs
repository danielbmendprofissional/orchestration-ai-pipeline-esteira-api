using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.ListarPessoas
{
    // Só o MOR procura pessoas (para vincular papel).
    public sealed record ListarPessoasQuery(PaginacaoDto Paginacao, string? Busca) : IRequest<Result<PaginaDto<PessoaListaDto>>>;

    public sealed class ListarPessoasHandler(IPessoaQuery query, IUsuarioAtual usuario)
        : IRequestHandler<ListarPessoasQuery, Result<PaginaDto<PessoaListaDto>>>
    {
        public Task<Result<PaginaDto<PessoaListaDto>>> Handle(ListarPessoasQuery request, CancellationToken cancellationToken)
            => usuario.EhMor
                ? query.ListarAsync(request.Paginacao, request.Busca, cancellationToken)
                : Task.FromResult(Result<PaginaDto<PessoaListaDto>>.Falha(AutorizacaoAplicacao.PapelInsuficiente));
    }
}
