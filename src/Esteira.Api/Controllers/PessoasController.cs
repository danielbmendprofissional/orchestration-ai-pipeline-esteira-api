using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Api.Paginacao;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Queries.ListarPessoas;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // A1.5: busca em Pessoa (quem já entrou na Esteira) para o MOR escolher quem vincular. Só MOR.
    [ApiController]
    [FeatureFlag("A1")]
    [Route("api/v1/pessoas")]
    public class PessoasController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<PessoaListaDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        public async Task<Result<PaginaDto<PessoaListaDto>>> Listar([FromQuery] string? busca, CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return Result<PaginaDto<PessoaListaDto>>.Falha(paginacao.Errors.ToArray());

            return await mediator.Send(new ListarPessoasQuery(paginacao.Value, busca), cancellationToken);
        }
    }
}
