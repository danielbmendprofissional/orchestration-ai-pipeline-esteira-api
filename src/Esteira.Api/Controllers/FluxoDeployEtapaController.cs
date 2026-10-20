using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaEspecificaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.AlterarEtapasFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterEtapasFluxoDeploy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployEtapaController : ControllerBase
    {

        private readonly IMediator _mediator;
        public FluxoDeployEtapaController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}/etapa")]
        public async Task<IActionResult> ObterEtapasFluxoDeploy([FromRoute] int id)
          => Ok(await _mediator.Send(new ObterEtapasFluxoDeployQuery { IdFluxoDeploy = id }));

        [HttpPut("{id}/etapa")]
        public async Task<IActionResult> AlterarEtapasFluxoDeploy([FromRoute] int id, [FromBody] List<string> etapas)
          => Ok(await _mediator.Send(new AlterarEtapasFluxoDeployCommand { Id = id, Etapas = etapas }));

        [HttpPut("{id}/etapa-seguinte")]
        public async Task<IActionResult> ALterarEtapaSeguinteFluxoDeploy([FromRoute] int id)
           => Ok(await _mediator.Send(new AlterarEtapaSeguinteFluxoDeployCommand { Id = id }));

        [HttpPut("{id}/etapa-especifica/{etapa}")]
        public async Task<IActionResult> AlterarEtapaEspecificaFluxoDeploy([FromRoute] int id, [FromRoute] string etapa)
          => Ok(await _mediator.Send(new AlterarEtapaEspecificaFluxoDeployCommand { Id = id, Etapa = etapa }));
    }
}
