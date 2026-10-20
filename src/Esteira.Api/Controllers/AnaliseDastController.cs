using Esteira.Application.Deploys.Queries.VerificarAnalisesDast;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/analise-dast")]
    [ApiController]
    public class AnaliseDastController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AnaliseDastController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPut]
        public async Task<IActionResult> VerificarAnalisesEmExecucao()
        {
            await _mediator.Send(new VerificarAnalisesDastQuery());
            return Ok();
        }
    }
}
