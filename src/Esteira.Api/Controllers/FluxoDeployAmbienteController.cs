using Esteira.Application.FluxosDeploy.Queries.ObterDetalheBranchFluxoDeploy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployAmbienteController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FluxoDeployAmbienteController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}/ambiente/{branch}")]
        public async Task<IActionResult> ObterFluxoDeploy([FromRoute] int id, [FromRoute] string branch)
           => Ok(await _mediator.Send(new ObterDetalheBranchFluxoDeployQuery { Id = id, Branch = branch }));
    }
}
