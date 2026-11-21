using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Queries.ObterPermissoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // A1: o front só lê as permissões, calculadas pelas mesmas regras das rotas de alteração (padrao-api).
    [ApiController]
    [FeatureFlag("A1")]
    [Route("api/v1/aplicacoes")]
    public class AplicacoesPermissoesController(IMediator mediator, IAutorizacaoAplicacao autorizacao) : ControllerBase
    {
        [HttpGet("permissoes")]
        [ProducesResponseType<PermissoesGlobaisAplicacao>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Globais() => Ok(new PermissoesGlobaisAplicacao(autorizacao.PodeCriar()));

        [HttpGet("{id:guid}/permissoes")]
        [ProducesResponseType<PermissoesAplicacao>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<PermissoesAplicacao>> DaAplicacao(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new ObterPermissoesAplicacaoQuery(id), cancellationToken);
    }
}
