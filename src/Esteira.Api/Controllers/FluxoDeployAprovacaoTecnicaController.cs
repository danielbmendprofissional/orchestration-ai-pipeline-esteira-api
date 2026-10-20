using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterAprovacoesTecnicasFluxoDeploy;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Esteira.Application.Interfaces.Usuario;

namespace Esteira.Api.Controllers
{
    [Route("api/fluxo-deploy")]
    [ApiController]
    public class FluxoDeployAprovacaoTecnicaController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUsuarioAtual _usuario;
        public FluxoDeployAprovacaoTecnicaController(IMediator mediator, IServiceProvider serviceProvider)
        {
            _mediator = mediator;
            _usuario = serviceProvider.GetRequiredService<IUsuarioAtual>();
        }


        [HttpGet("{id}/aprovacao-tecnica")]
        public async Task<IActionResult> ObterAprovacoes([FromRoute] int id)
            => Ok(
                await _mediator.Send(
                    new ObterAprovacoesTecnicasFluxoDeployQuery() { IdFluxoDeploy = id }
                )
            );


        [HttpPost("{id}/aprovacao-tecnica")]
        public async Task<ActionResult> Aprovar([FromRoute] int id, [FromBody] AprovacaoTecnicaModel model)
            => Ok(
                    await _mediator.Send(
                        new CriarAprovacaoTecnicaFluxoDeployCommand(
                            id,
                            _usuario.Usuario,
                            model.Aprovado.ToCharArray().First(),
                            model.Justificativa
                        )
                    )
                );
    }
}
