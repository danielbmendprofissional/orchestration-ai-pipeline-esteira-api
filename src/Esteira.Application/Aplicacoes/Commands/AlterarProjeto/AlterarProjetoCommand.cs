using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.AlterarProjeto
{
    // Ativo é nullable de propósito: omitir o campo não pode desativar o Projeto.
    public sealed record AlterarProjetoCommand(Guid ProjetoId, string Stack, bool? Ativo)
        : IRequest<Result<ProjetoResumoDto>>, ITransactionRequest;

    public sealed class AlterarProjetoValidator : AbstractValidator<AlterarProjetoCommand>
    {
        public AlterarProjetoValidator()
        {
            RuleFor(x => x.Stack)
                .Must(s => EnumKebab.TentarLer<Stack>(s, out _))
                .WithErrorCode("projeto.stack-invalida")
                .WithMessage("Stack inválida. Use dotnet, angular ou outro.");
            RuleFor(x => x.Ativo).NotNull().WithErrorCode("projeto.ativo-obrigatorio").WithMessage("Informe se o Projeto está ativo.");
        }
    }

    public sealed class AlterarProjetoHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IAuditoria auditoria)
        : IRequestHandler<AlterarProjetoCommand, Result<ProjetoResumoDto>>
    {
        public async Task<Result<ProjetoResumoDto>> Handle(AlterarProjetoCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            EnumKebab.TentarLer<Stack>(request.Stack, out var stack);

            var antes = Estado(projeto);
            projeto.Alterar(stack, request.Ativo!.Value);
            auditoria.Registrar("Projeto", projeto.PublicId, AcaoAuditoria.Alterou, antes, Estado(projeto), new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));

            return Result<ProjetoResumoDto>.Sucesso(await projetos.MontarDtoAsync(projeto, cancellationToken));
        }

        private static object Estado(ProjetoEntity p) => new { Stack = EnumKebab.Texto(p.Stack), p.Ativo };
    }
}
