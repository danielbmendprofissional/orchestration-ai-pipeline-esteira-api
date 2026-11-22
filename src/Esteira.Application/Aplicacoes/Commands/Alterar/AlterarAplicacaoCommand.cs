using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.Alterar
{
    // Ativa é nullable de propósito: omitir o campo não pode desativar a Aplicação.
    public sealed record AlterarAplicacaoCommand(Guid Id, string Nome, string Sigla, bool? Ativa)
        : IRequest<Result<AplicacaoListaDto>>, ITransactionRequest;

    public sealed class AlterarAplicacaoValidator : AbstractValidator<AlterarAplicacaoCommand>
    {
        public AlterarAplicacaoValidator()
        {
            RuleFor(x => x.Nome).NotEmpty().WithErrorCode("aplicacao.nome-obrigatorio").WithMessage("Informe o nome da Aplicação.");
            RuleFor(x => x.Sigla).NotEmpty().WithErrorCode("aplicacao.sigla-obrigatoria").WithMessage("Informe a sigla da Aplicação.");
            RuleFor(x => x.Ativa).NotNull().WithErrorCode("aplicacao.ativa-obrigatoria").WithMessage("Informe se a Aplicação está ativa.");
        }
    }

    public sealed class AlterarAplicacaoHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio repositorio,
        IUnitOfWork uow,
        IAuditoria auditoria) : IRequestHandler<AlterarAplicacaoCommand, Result<AplicacaoListaDto>>
    {
        public async Task<Result<AplicacaoListaDto>> Handle(AlterarAplicacaoCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.Id, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.EditarAplicacao, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<AplicacaoListaDto>.Falha(autorizado.Errors.ToArray());

            var aplicacao = await repositorio.ObterAsync(id.Value, cancellationToken);
            if (aplicacao is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var sigla = AplicacaoEntity.NormalizarSigla(request.Sigla);
            if (await repositorio.SiglaExisteAsync(sigla, id.Value, cancellationToken))
                return Result<AplicacaoListaDto>.Falha(AplicacaoErros.SiglaDuplicada);

            var antes = Estado(aplicacao);
            var alterada = aplicacao.Alterar(request.Nome, sigla, request.Ativa!.Value);
            if (!alterada.IsSuccess)
                return Result<AplicacaoListaDto>.Falha(alterada.Errors.ToArray());

            auditoria.Registrar("Aplicacao", aplicacao.PublicId, AcaoAuditoria.Alterou, antes, Estado(aplicacao));

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (AplicacaoErros.DaViolacao(violacao) is not null)
            {
                return Result<AplicacaoListaDto>.Falha(AplicacaoErros.DaViolacao(violacao)!);
            }

            return Result<AplicacaoListaDto>.Sucesso(AplicacaoErros.Lista(aplicacao));
        }

        private static object Estado(AplicacaoEntity a) => new { a.Nome, a.Sigla, a.Ativa };
    }
}
