using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Application.Entregas.Queries.Obter;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // E1b (tela da Entrega). Desligada, a rota nem casa (404 de rota). Controller fino: o ResultadoFilter traduz o Result em HTTP.
    [ApiController]
    [FeatureFlag("E1b")]
    [Route("api/v1/entregas")]
    public class EntregasController(IMediator mediator) : ControllerBase
    {
        [HttpGet("{id:guid}")]
        [ProducesResponseType<EntregaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<EntregaDto>> Obter(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new ObterEntregaQuery(id), cancellationToken);
    }
}
