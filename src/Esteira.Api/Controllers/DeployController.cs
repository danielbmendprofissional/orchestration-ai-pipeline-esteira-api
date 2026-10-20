using Esteira.Application.Deploys.Queries.ObterArtefatosAnalise;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Esteira.Api.Paginacao;

namespace Esteira.Api.Controllers
{
    [Route("api/deploy")]
    [ApiController]
    public class DeployController : ControllerBase
    {

        private readonly IMediator _mediator;

        public DeployController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}/artefatos")]
        public async Task<IActionResult> ObterTodos([FromRoute] int id)
            => Ok(await _mediator.Send(new ObterArtefatosAnaliseQuery { Pagination = HttpContext.ObterPaginacao(), IdDeploy = id }));



    }
}
