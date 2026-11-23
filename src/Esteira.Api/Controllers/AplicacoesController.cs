using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Api.Paginacao;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.Alterar;
using Esteira.Application.Aplicacoes.Commands.AlterarPolitica;
using Esteira.Application.Aplicacoes.Commands.CadastrarProjeto;
using Esteira.Application.Aplicacoes.Commands.DefinirWorkspace;
using Esteira.Application.Aplicacoes.Commands.Criar;
using Esteira.Application.Aplicacoes.Commands.Papeis;
using Esteira.Application.Aplicacoes.Queries.Listar;
using Esteira.Application.Aplicacoes.Queries.ListarPapeis;
using Esteira.Application.Aplicacoes.Queries.ListarProjetosGitLab;
using Esteira.Application.Aplicacoes.Queries.Obter;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // A1 (cadastro de Aplicação). Controller fino: monta command/query e devolve o Result; quem traduz em HTTP é o ResultadoFilter.
    // Campos de corpo são anuláveis de propósito: a ausência cai nos validators (code estável), não no 400 genérico do ModelState.
    [ApiController]
    [FeatureFlag("A1")]
    [Route("api/v1/aplicacoes")]
    public class AplicacoesController(IMediator mediator) : ControllerBase
    {
        public sealed record CriarAplicacaoRequest(string? Nome, string? Sigla, string? GrupoGitLab);

        public sealed record AlterarAplicacaoRequest(string? Nome, string? Sigla, bool? Ativa);

        public sealed record CadastrarProjetoRequest(long? ProjetoGitLabId, string? Stack);

        public sealed record DefinirWorkspaceRequest(long? ProjetoGitLabId);

        public sealed record VincularPapelRequest(string? Usuario, string? Papel);

        public sealed record AlterarPoliticaRequest(int? Critica, int? Alta, int? Media, int? Baixa, string? Modo);

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<AplicacaoListaDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<Result<PaginaDto<AplicacaoListaDto>>> Listar([FromQuery] string? busca, [FromQuery] bool? ativa, CancellationToken cancellationToken)
        {
            var paginacao = Request.ObterPaginacao();
            if (!paginacao.IsSuccess)
                return Result<PaginaDto<AplicacaoListaDto>>.Falha(paginacao.Errors.ToArray());

            return await mediator.Send(new ListarAplicacoesQuery(paginacao.Value, busca, ativa), cancellationToken);
        }

        [HttpPost]
        [ProducesResponseType<AplicacaoListaDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Criar([FromBody] CriarAplicacaoRequest corpo, CancellationToken cancellationToken)
        {
            var resultado = await mediator.Send(
                new CriarAplicacaoCommand(corpo.Nome ?? string.Empty, corpo.Sigla ?? string.Empty, corpo.GrupoGitLab ?? string.Empty), cancellationToken);

            return resultado.IsSuccess
                ? Created($"/api/v1/aplicacoes/{resultado.Value.Id}", resultado.Value)
                : new ObjectResult(resultado);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<AplicacaoDetalheDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<AplicacaoDetalheDto>> Obter(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new ObterAplicacaoQuery(id), cancellationToken);

        [HttpPut("{id:guid}")]
        [ProducesResponseType<AplicacaoListaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        public async Task<Result<AplicacaoListaDto>> Alterar(Guid id, [FromBody] AlterarAplicacaoRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(new AlterarAplicacaoCommand(id, corpo.Nome ?? string.Empty, corpo.Sigla ?? string.Empty, corpo.Ativa), cancellationToken);

        // P1.2: workspace da Aplicação. Flag própria Features:P1 (além da A1 do controller). `{ projetoGitLabId: null }` limpa.
        [HttpPut("{id:guid}/workspace")]
        [FeatureFlag("P1")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<Result> DefinirWorkspace(Guid id, [FromBody] DefinirWorkspaceRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(new DefinirWorkspaceCommand(id, corpo.ProjetoGitLabId), cancellationToken);

        [HttpGet("{id:guid}/gitlab/projetos")]
        [ProducesResponseType<IReadOnlyList<ProjetoGitLabItemDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<Result<IReadOnlyList<ProjetoGitLabItemDto>>> ListarProjetosGitLab(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new ListarProjetosGitLabQuery(id), cancellationToken);

        [HttpPost("{id:guid}/projetos")]
        [ProducesResponseType<ProjetoResumoDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CadastrarProjeto(Guid id, [FromBody] CadastrarProjetoRequest corpo, CancellationToken cancellationToken)
        {
            var resultado = await mediator.Send(new CadastrarProjetoCommand(id, corpo.ProjetoGitLabId, corpo.Stack ?? string.Empty), cancellationToken);

            return resultado.IsSuccess
                ? Created($"/api/v1/projetos/{resultado.Value.Id}", resultado.Value)
                : new ObjectResult(resultado);
        }

        // Papéis (A1.5): leitura com papel na Aplicação ou MOR; vincular/desvincular só MOR. Lista pequena, sem paginação.
        [HttpGet("{id:guid}/papeis")]
        [ProducesResponseType<IReadOnlyList<PapelResumoDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<IReadOnlyList<PapelResumoDto>>> ListarPapeis(Guid id, CancellationToken cancellationToken)
            => await mediator.Send(new ListarPapeisQuery(id), cancellationToken);

        [HttpPost("{id:guid}/papeis")]
        [ProducesResponseType<PapelResumoDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> VincularPapel(Guid id, [FromBody] VincularPapelRequest corpo, CancellationToken cancellationToken)
        {
            var resultado = await mediator.Send(new VincularPapelCommand(id, corpo.Usuario ?? string.Empty, corpo.Papel ?? string.Empty), cancellationToken);

            return resultado.IsSuccess
                ? Created($"/api/v1/aplicacoes/{id}/papeis", resultado.Value)
                : new ObjectResult(resultado);
        }

        [HttpDelete("{id:guid}/papeis/{papelId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result> DesvincularPapel(Guid id, Guid papelId, CancellationToken cancellationToken)
            => await mediator.Send(new DesvincularPapelCommand(id, papelId), cancellationToken);

        [HttpPut("{id:guid}/politicas/{ambiente}")]
        [ProducesResponseType<PoliticaDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<Result<PoliticaDto>> AlterarPolitica(Guid id, string ambiente, [FromBody] AlterarPoliticaRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(
                new AlterarPoliticaCommand(id, ambiente, corpo.Critica, corpo.Alta, corpo.Media, corpo.Baixa, corpo.Modo ?? string.Empty), cancellationToken);
    }
}
