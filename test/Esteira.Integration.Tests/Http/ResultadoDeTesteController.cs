using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Integration.Tests.Http
{
    // Controller só de teste: fino, como os de verdade — monta o request e devolve o Result.
    // A tradução Result → HTTP é do filtro, então aqui não há status na mão.
    [ApiController]
    [Route("api/teste")]
    public sealed class ResultadoDeTesteController(IMediator mediator) : ControllerBase
    {
        [HttpGet("valor")]
        public async Task<Result<string>> ComValor(CancellationToken cancellationToken)
            => await mediator.Send(new PedidoComValor(), cancellationToken);

        [HttpGet("sem-valor")]
        public async Task<Result> SemValor(CancellationToken cancellationToken)
            => await mediator.Send(new PedidoSemValor(), cancellationToken);

        [HttpGet("falha/{tipo}")]
        public async Task<Result<string>> ComFalha(TipoErro tipo, CancellationToken cancellationToken)
            => await mediator.Send(new PedidoComFalha(tipo), cancellationToken);

        [HttpGet("duas-falhas")]
        public async Task<Result<string>> ComDuasFalhas(CancellationToken cancellationToken)
            => await mediator.Send(new PedidoComDuasFalhas(), cancellationToken);

        [HttpPost("validado")]
        public async Task<Result<string>> Validado([FromBody] PedidoValidado pedido, CancellationToken cancellationToken)
            => await mediator.Send(pedido, cancellationToken);

        [HttpPost("sem-error-code")]
        public async Task<Result<string>> SemErrorCode([FromBody] PedidoSemErrorCode pedido, CancellationToken cancellationToken)
            => await mediator.Send(pedido, cancellationToken);

        [HttpPost("corpo")]
        public async Task<Result<string>> ComCorpo([FromBody] PedidoComCorpo pedido, CancellationToken cancellationToken)
            => await mediator.Send(pedido, cancellationToken);
    }
}
