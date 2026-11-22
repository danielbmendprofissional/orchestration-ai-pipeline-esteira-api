using Esteira.Api.Erros;
using Esteira.Api.Features;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.AlterarAmbiente;
using Esteira.Application.Aplicacoes.Commands.AlterarProjeto;
using Esteira.Application.Aplicacoes.Commands.Destinos;
using Esteira.Application.Aplicacoes.Commands.SubstituirDependencias;
using Esteira.Domain.Common.Resultados;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // A1.4: Projeto, dependências, Ambientes e Destinos. Cadastro de Projeto e lista do GitLab ficam em /aplicacoes/{id}.
    // Campos de corpo anuláveis de propósito: a ausência cai nos validators (code estável), não no 400 genérico do ModelState.
    [ApiController]
    [FeatureFlag("A1")]
    [Route("api/v1/projetos")]
    public class ProjetosController(IMediator mediator) : ControllerBase
    {
        public sealed record AlterarProjetoRequest(string? Stack, bool? Ativo);

        public sealed record DependenciasRequest(List<Guid>? DependeDe);

        public sealed record AlterarAmbienteRequest(bool? Habilitado);

        public sealed record DestinoRequest(string? Nome, Dictionary<string, string>? Configuracao, int? Ordem);

        [HttpPut("{id:guid}")]
        [ProducesResponseType<ProjetoResumoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<ProjetoResumoDto>> Alterar(Guid id, [FromBody] AlterarProjetoRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(new AlterarProjetoCommand(id, corpo.Stack ?? string.Empty, corpo.Ativo), cancellationToken);

        [HttpPut("{id:guid}/dependencias")]
        [ProducesResponseType<ProjetoResumoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<ProjetoResumoDto>> SubstituirDependencias(Guid id, [FromBody] DependenciasRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(new SubstituirDependenciasCommand(id, corpo.DependeDe), cancellationToken);

        [HttpPut("{id:guid}/ambientes/{ambiente}")]
        [ProducesResponseType<ProjetoResumoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result<ProjetoResumoDto>> AlterarAmbiente(Guid id, string ambiente, [FromBody] AlterarAmbienteRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(new AlterarAmbienteCommand(id, ambiente, corpo.Habilitado), cancellationToken);

        [HttpPost("{id:guid}/ambientes/{ambiente}/destinos")]
        [ProducesResponseType<DestinoDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CriarDestino(Guid id, string ambiente, [FromBody] DestinoRequest corpo, CancellationToken cancellationToken)
        {
            var resultado = await mediator.Send(
                new CriarDestinoCommand(id, ambiente, corpo.Nome ?? string.Empty, corpo.Configuracao, corpo.Ordem), cancellationToken);

            return resultado.IsSuccess
                ? Created($"/api/v1/projetos/{id}/ambientes/{ambiente}/destinos/{resultado.Value.Id}", resultado.Value)
                : new ObjectResult(resultado);
        }

        [HttpPut("{id:guid}/ambientes/{ambiente}/destinos/{destinoId:guid}")]
        [ProducesResponseType<DestinoDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status409Conflict)]
        public async Task<Result<DestinoDto>> AlterarDestino(
            Guid id, string ambiente, Guid destinoId, [FromBody] DestinoRequest corpo, CancellationToken cancellationToken)
            => await mediator.Send(
                new AlterarDestinoCommand(id, ambiente, destinoId, corpo.Nome ?? string.Empty, corpo.Configuracao, corpo.Ordem), cancellationToken);

        [HttpDelete("{id:guid}/ambientes/{ambiente}/destinos/{destinoId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status404NotFound)]
        public async Task<Result> RemoverDestino(Guid id, string ambiente, Guid destinoId, CancellationToken cancellationToken)
            => await mediator.Send(new RemoverDestinoCommand(id, ambiente, destinoId), cancellationToken);
    }
}
