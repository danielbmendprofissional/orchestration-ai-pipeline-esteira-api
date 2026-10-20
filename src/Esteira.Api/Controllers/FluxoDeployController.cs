using Esteira.Application.FluxosDeploy.Commands.CancelarFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterListaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterResumoFluxoDeploy;
using Esteira.Domain.Common.Enums;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Api.Paginacao;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUsuarioAtual _usuario;

        public FluxoDeployController(IMediator mediator, IUsuarioAtual usuario)
        {
            _mediator = mediator;
            _usuario = usuario;
        }

        [HttpPost]
        public async Task<IActionResult> InserirFluxoDeploy([FromBody] FluxoDeployModel fluxoDeploy)
            => Ok(await _mediator.Send(new CriarFluxoDeployCommand(fluxoDeploy.Sistema, _usuario.Usuario, fluxoDeploy.Etapas)));

        [HttpGet]
        public async Task<IActionResult> ObterLista([FromQuery] string sistema, [FromQuery] DateOnly? dataInicio, [FromQuery] DateOnly? dataFim, [FromQuery] StatusEnum? status)
            => Ok(await _mediator.Send(new ObterListaFluxoDeployQuery { Pagination = HttpContext.ObterPaginacao(), Sistema = sistema, DataInicio = dataInicio, DataFim = dataFim, Status = status }));

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelarFluxoDeploy([FromRoute] int id)
           => Ok(await _mediator.Send(new CancelarFluxoDeployCommand { Id = id }));


        [HttpGet("{id}/resumo")]
        public async Task<IActionResult> ObterResumoFluxoDeploy([FromRoute] int id)
            => Ok(await _mediator.Send(new ObterResumoFluxoDeployQuery { Id = id }));

    }
}
