using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Api.Paginacao;
using Esteira.Application.Auditoria;
using Esteira.Application.Auditoria.Queries;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // X1.3: leitura da trilha. Só GET (a trilha é imutável); o escopo por papel vale em todas as rotas
    // (IEscopoDeAuditoria). Flag ausente ou desligada = 404.
    [ApiController]
    [FeatureFlag("X1")]
    [Route("api/v1/auditoria")]
    public class AuditoriaController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<RegistroAuditoriaListaDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        public async Task<Result<PaginaDto<RegistroAuditoriaListaDto>>> Listar(
            [FromQuery] Guid? aplicacaoId, [FromQuery] string? quem, [FromQuery] string? categoria,
            [FromQuery] int? periodoDias, [FromQuery] string? objeto, [FromQuery] string? busca, CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return Result<PaginaDto<RegistroAuditoriaListaDto>>.Falha(paginacao.Errors.ToArray());

            var filtro = FiltroDeAuditoria.Criar(aplicacaoId, quem, categoria, periodoDias, objeto, busca);
            if (!filtro.IsSuccess)
                return Result<PaginaDto<RegistroAuditoriaListaDto>>.Falha(filtro.Errors.ToArray());

            return await mediator.Send(new ListarAuditoriaQuery(paginacao.Value, filtro.Value), cancellationToken);
        }

        [HttpGet("categorias")]
        [ProducesResponseType<IReadOnlyList<CategoriaContagemDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        public async Task<Result<IReadOnlyList<CategoriaContagemDto>>> Categorias(
            [FromQuery] Guid? aplicacaoId, [FromQuery] string? quem, [FromQuery] string? categoria,
            [FromQuery] int? periodoDias, [FromQuery] string? objeto, [FromQuery] string? busca, CancellationToken cancellationToken)
        {
            var filtro = FiltroDeAuditoria.Criar(aplicacaoId, quem, categoria, periodoDias, objeto, busca);
            if (!filtro.IsSuccess)
                return Result<IReadOnlyList<CategoriaContagemDto>>.Falha(filtro.Errors.ToArray());

            return await mediator.Send(new ContarAuditoriaPorCategoriaQuery(filtro.Value), cancellationToken);
        }

        [HttpGet("filtros")]
        [ProducesResponseType<FiltrosDeAuditoriaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        public Task<Result<FiltrosDeAuditoriaDto>> Filtros(CancellationToken cancellationToken)
            => mediator.Send(new ObterFiltrosDeAuditoriaQuery(), cancellationToken);
    }
}
