using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Api.Paginacao;
using Esteira.Application.Fatias;
using Esteira.Application.Fatias.Consultas;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // P1 (Painel de Fatias). Só leitura: nenhuma rota escreve no GitLab. Flag `Features:P1`; ausente = 404.
    // Sem papel na Aplicação (e sem MOR) a Aplicação não existe para a pessoa: 404, nunca 403.
    [ApiController]
    [FeatureFlag("P1")]
    [Route("api/v1")]
    public class FatiasController(IMediator mediator) : ControllerBase
    {
        // Lista paginada em memória sobre o instantâneo (ADR-0002). Diferente das outras listas, o body é um envelope
        // (`itens`, `avisos`, `aplicacoes`, `geradoEm`, `desatualizado`) porque o front precisa dos avisos por Aplicação,
        // inclusive das que não têm nenhuma Fatia; a página continua nos headers X-*.
        [HttpGet("fatias")]
        [ProducesResponseType<PainelListaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? aba, [FromQuery] string? fatia, [FromQuery] string? projeto, [FromQuery] string? aplicacoes,
            CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return new ObjectResult(Result<PainelListaDto>.Falha(paginacao.Errors.ToArray()));

            var resultado = await mediator.Send(new ListarFatiasQuery(paginacao.Value, aba, fatia, projeto, aplicacoes), cancellationToken);
            if (!resultado.IsSuccess)
                return new ObjectResult(resultado);

            var lista = resultado.Value;
            Response.Headers[HeadersPaginacao.Total] = lista.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Response.Headers[HeadersPaginacao.TotalPaginas] = lista.TotalPaginas.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Response.Headers[HeadersPaginacao.Pagina] = lista.Pagina.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Response.Headers[HeadersPaginacao.TamanhoPagina] = lista.TamanhoPagina.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Ok(lista);
        }

        [HttpPost("aplicacoes/{id:guid}/fatias/atualizar")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status429TooManyRequests)]
        public async Task<Result> Atualizar(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new AtualizarFatiasCommand(id), cancellationToken);

        [HttpGet("aplicacoes/{id:guid}/fatias/{codigo}")]
        [ProducesResponseType<GavetaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<GavetaDto>> Gaveta(Guid id, string codigo, CancellationToken cancellationToken)
            => await mediator.Send(new ObterGavetaQuery(id, codigo), cancellationToken);
    }
}
