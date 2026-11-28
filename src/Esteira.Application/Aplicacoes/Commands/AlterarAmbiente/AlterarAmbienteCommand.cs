using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.AlterarAmbiente
{
    // Ambiente chega em texto kebab-case (`qa`, `hml`, `producao`); `dev` e `review` não são cadastráveis (ADR-0020/0021).
    public sealed record AlterarAmbienteCommand(Guid ProjetoId, string Ambiente, bool? Habilitado)
        : IRequest<Result<ProjetoResumoDto>>, ITransactionRequest;

    public sealed class AlterarAmbienteValidator : AbstractValidator<AlterarAmbienteCommand>
    {
        public AlterarAmbienteValidator()
        {
            RuleFor(x => x.Ambiente)
                .Must(a => EnumKebab.TentarLer<AmbienteDeploy>(a, out _))
                .WithErrorCode("ambiente.invalido")
                .WithMessage("Ambiente inválido. Use qa, hml ou producao.");
            RuleFor(x => x.Habilitado).NotNull().WithErrorCode("ambiente.habilitado-obrigatorio").WithMessage("Informe se o ambiente está habilitado.");
        }
    }

    public sealed class AlterarAmbienteHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IAuditoria auditoria)
        : IRequestHandler<AlterarAmbienteCommand, Result<ProjetoResumoDto>>
    {
        public async Task<Result<ProjetoResumoDto>> Handle(AlterarAmbienteCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            EnumKebab.TentarLer<AmbienteDeploy>(request.Ambiente, out var ambiente);

            var linha = await projetos.ObterAmbienteAsync(projeto.Id, ambiente, cancellationToken);
            if (linha is null)
                return ProjetoErros.AmbienteNaoEncontrado;

            var antes = Estado(projeto, linha);
            var alterado = linha.Alterar(request.Habilitado!.Value);
            if (!alterado.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(alterado.Errors.ToArray());

            auditoria.Registrar("AmbienteProjeto", linha.PublicId, AcaoAuditoria.Alterou, antes, Estado(projeto, linha), new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));

            return Result<ProjetoResumoDto>.Sucesso(await projetos.MontarDtoAsync(projeto, cancellationToken));
        }

        private static object Estado(ProjetoEntity projeto, AmbienteProjetoEntity a)
            => new { Projeto = projeto.PublicId, Ambiente = EnumKebab.Texto(a.Ambiente), a.Habilitado };
    }
}
