using Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterGmudFluxoDeploy;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Esteira.Application.Interfaces.Usuario;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployVinculosController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUsuarioAtual _usuario;
        public FluxoDeployVinculosController(IMediator mediator, IUsuarioAtual usuario)
        {
            _mediator = mediator;
            _usuario = usuario;
        }

        [HttpPost("{id}/versao")]
        public async Task<IActionResult> VincularVersao([FromRoute] int id, [FromBody] VersaoTarefasModel model)
        => Ok(
                await _mediator.Send(
                    new CriarVersaoTarefasFluxoDeployCommand(
                        id,
                        model.IdProjeto,
                        model.IdVersao
                    )
                )
            );


        [HttpGet("{id}/gmud")]
        public async Task<IActionResult> ObterGmud([FromRoute] int id)
        {
            return Ok(
             await _mediator.Send(
                     new ObterGmudFluxoDeployQuery(
                         id
                     )
                 )
             );
        }

        [HttpPost("{id}/gmud")]
        public async Task<IActionResult> CriarGmud([FromRoute] int id, [FromBody] GmudModel model)
        {

            return Ok(
                 await _mediator.Send(
                     new CriarGmudFluxoDeployCommand(
                         id,
                         _usuario.Usuario,
                         model.Justificativa,
                         model.IdGmud
                     )
                 )
            );
        }

    }
}
