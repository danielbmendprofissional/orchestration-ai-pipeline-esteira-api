using Esteira.Application.Status.Queries.ObterStatus;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StatusController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> ObterStatus()
            => Ok(await _mediator.Send(new ObterStatusQuery()));
    }


}
