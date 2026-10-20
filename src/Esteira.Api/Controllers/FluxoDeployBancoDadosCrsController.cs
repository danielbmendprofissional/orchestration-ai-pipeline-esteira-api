using Esteira.Application.DTOs.Request;
using Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.RemoverCrsBdFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterCrsBdFluxoDeploy;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Esteira.Api.Paginacao;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployBancoDadosCrsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FluxoDeployBancoDadosCrsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}/crs-bd")]
        public async Task<IActionResult> ObterCrs([FromRoute] int id)
            => Ok(
                await _mediator.Send(
                    new ObterCrsBdFluxoDeployQuery
                    {
                        IdFluxoDeploy = id,
                        Pagination = HttpContext.ObterPaginacao()
                    }
                )
            );

        [HttpPost("{id}/crs-bd")]
        public async Task<IActionResult> VincularCrsBancoDeDados([FromRoute] int id, [FromBody] List<CrsBdModel> model)
            => Ok(
                await _mediator.Send(
                    new CriarCrsBdFluxoDeployCommand
                    {
                        IdFluxoDeploy = id,
                        CrsBd = model.Select(a =>
                                                new BdCrsDtoRequest
                                                {
                                                    Crs = a.Crs,
                                                    Url = a.Url
                                                }).ToList()

                    }
                )
            );

        [HttpDelete("{id}/crs-bd/{idCrs}")]
        public async Task<IActionResult> DesvincularCrsBancoDeDados([FromRoute] int id, [FromRoute] int idCrs)
            => Ok(
                await _mediator.Send(
                    new RemoverCrsBdFluxoDeployCommand
                    {
                        IdFluxoDeploy = id,
                        IdCrs = idCrs,
                    }
                )
            );
    }
}
