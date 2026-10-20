using Esteira.Application.Etapas.Commands.AlterarStatusEtapa;
using Esteira.Application.Etapas.Commands.CriarEtapa;
using Esteira.Application.Etapas.Queries.ObterListaEtapa;
using Esteira.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Esteira.Api.Paginacao;

namespace Esteira.Api.Controllers
{
    [Route("api/etapa")]
    [ApiController]
    public class EtapaController : ControllerBase
    {
        private readonly IMediator _mediator;

        public EtapaController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> ObterLista()
            => Ok(await _mediator.Send(new ObterListaEtapaQuery()));

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] EtapaModel model)
            => Ok(await _mediator.Send(new CriarEtapaCommand
            {
                Sigla = model.Sigla,
                Descricao = model.Descricao,
                Obrigatorio = model.Obrigatorio.ToCharArray().First(),
            }));


        [HttpPatch("{sigla}")]
        public async Task<IActionResult> AlterarStatus(
            [FromRoute] string sigla,
            [FromBody] AlterarStatusModel model)
        {
            await _mediator.Send(new AlterarStatusEtapaCommand(sigla, model.Status));
            return NoContent();
        }

    }
}
