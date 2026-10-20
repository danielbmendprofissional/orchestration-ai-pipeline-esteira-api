using Esteira.Application.DTOs.Request;
using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.RemoverAprovacaoNegocioFluxoDeploy;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployAprovacaoNegocioController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FluxoDeployAprovacaoNegocioController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{id}/aprovacao-negocio")]
        public async Task<ActionResult> VincularTarefas([FromRoute] int id, [FromBody] List<AprovacaoNegocioModel> model)
            => Ok(
                await _mediator.Send(
                    new CriarAprovacaoNegocioFluxoDeployCommand
                    {
                        IdFluxoDeploy = id,
                        AprovacoesNegocio = model.Select(a =>
                                                new AprovacaoNegocioDtoRequest
                                                {
                                                    Tarefa = a.Id,
                                                    Url = a.Url
                                                }).ToList()
                    }

                    )

                );

        [HttpDelete("{id}/aprovacao-negocio/{idTarefa}")]
        public async Task<IActionResult> DesvincularTarefa([FromRoute] int id, [FromRoute] int idTarefa)
            => Ok(
                await _mediator.Send(
                    new RemoverAprovacaoNegocioFluxoDeployCommand(id, idTarefa)
                )
            );

    }


}
