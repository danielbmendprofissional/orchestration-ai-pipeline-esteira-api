using Esteira.Application.AprovadoresTecnicos.Queries.ObterEquipeAprovacaoTecnica;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/aprovacao-tecnica")]
    [ApiController]
    public class AprovacaoTecnicaController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AprovacaoTecnicaController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("equipe/{equipe}")]
        public async Task<IActionResult> ObterAprovacao([FromRoute] string equipe)
            => Ok(
                await _mediator.Send(
                    new ObterEquipeAprovacaoTecnicaQuery() { Equipe = equipe }
                )
            );
    }
}
