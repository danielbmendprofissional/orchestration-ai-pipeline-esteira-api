using Esteira.Api.Paginacao;
using Esteira.Application.Common.Paginacao;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Integration.Tests.Paginacao
{
    // Controller e query só de teste sobre o ContextoDePaginacao: fino como os de verdade —
    // lê a paginação, devolve a falha sem chamar o MediatR, senão manda a query e devolve o Result.
    public sealed record ListarItensDeTeste(PaginacaoDto Paginacao) : IRequest<Result<PaginaDto<ItemDeTesteDto>>>;

    public sealed class ListarItensDeTesteHandler(ContextoDePaginacao contexto)
        : IRequestHandler<ListarItensDeTeste, Result<PaginaDto<ItemDeTesteDto>>>
    {
        // Zerado pelo teste: prova que o pedido inválido não chegou à consulta.
        public static int Execucoes { get; set; }

        public Task<Result<PaginaDto<ItemDeTesteDto>>> Handle(ListarItensDeTeste request, CancellationToken cancellationToken)
        {
            Execucoes++;
            return contexto.Itens.PaginarAsync(
                request.Paginacao,
                i => new ItemDeTesteDto { Id = i.Id, Nome = i.Nome, Grupo = i.Grupo },
                ContextoDePaginacao.OrdenacaoPadrao,
                cancellationToken);
        }
    }

    [ApiController]
    [Route("api/teste/itens")]
    public sealed class ItensDeTesteController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<Result<PaginaDto<ItemDeTesteDto>>> Listar(CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return Result<PaginaDto<ItemDeTesteDto>>.Falha(paginacao.Errors.ToArray());

            return await mediator.Send(new ListarItensDeTeste(paginacao.Value), cancellationToken);
        }
    }
}
