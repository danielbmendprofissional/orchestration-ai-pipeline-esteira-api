using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Api.Paginacao;
using Esteira.Application.Analises.Queries;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // E2 (leitura das Análises e Achados). Só a leitura leva a flag: desligada, a rota nem casa (404 de rota).
    // O gate e o resultado da pipeline não têm flag (um gate "desligado" seria bypass).
    [ApiController]
    [FeatureFlag("E2")]
    [Route("api/v1/entregas/{entregaId:guid}/fluxos/{fluxoId:guid}")]
    public class AnalisesController(IMediator mediator) : ControllerBase
    {
        [HttpGet("etapas/{etapa}/analises")]
        [ProducesResponseType<AnalisesDaEtapaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<AnalisesDaEtapaDto>> DaEtapa(Guid entregaId, Guid fluxoId, string etapa, CancellationToken cancellationToken)
            => await mediator.Send(new ObterAnalisesDaEtapaQuery(entregaId, fluxoId, etapa), cancellationToken);

        [HttpGet("analises/{analiseId:guid}/achados")]
        [ProducesResponseType<IReadOnlyList<AchadoDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<PaginaDto<AchadoDto>>> Achados(
            Guid entregaId, Guid fluxoId, Guid analiseId, [FromQuery] string? severidade, CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return Result<PaginaDto<AchadoDto>>.Falha(paginacao.Errors.ToArray());

            return await mediator.Send(new ListarAchadosQuery(entregaId, fluxoId, analiseId, severidade, paginacao.Value), cancellationToken);
        }
    }
}
