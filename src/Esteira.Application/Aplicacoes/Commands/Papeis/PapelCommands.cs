using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.Papeis
{
    // Corpo `{ usuario (sub), papel }`; campos anuláveis no controller, a ausência cai nos validators.
    public sealed record VincularPapelCommand(Guid AplicacaoId, string Usuario, string Papel)
        : IRequest<Result<PapelResumoDto>>, ITransactionRequest;

    public sealed record DesvincularPapelCommand(Guid AplicacaoId, Guid PapelId)
        : IRequest<Result>, ITransactionRequest;

    public sealed class VincularPapelValidator : AbstractValidator<VincularPapelCommand>
    {
        public VincularPapelValidator()
        {
            RuleFor(x => x.Usuario).NotEmpty().WithErrorCode("papel.usuario-obrigatorio").WithMessage("Informe a pessoa.");
            RuleFor(x => x.Papel)
                .Must(p => EnumKebab.TentarLer<PapelAplicacao>(p, out _))
                .WithErrorCode("papel.papel-invalido")
                .WithMessage("Papel inválido. Use analista, membro, aprovador-tecnico, aprovador-negocio ou publicador.");
        }
    }

    public sealed class VincularPapelHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IPessoaRepositorio pessoas,
        IPapelRepositorio papeis,
        IUnitOfWork uow,
        IAuditoria auditoria) : IRequestHandler<VincularPapelCommand, Result<PapelResumoDto>>
    {
        public async Task<Result<PapelResumoDto>> Handle(VincularPapelCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.GerenciarPapeis, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<PapelResumoDto>.Falha(autorizado.Errors.ToArray());

            EnumKebab.TentarLer<PapelAplicacao>(request.Papel, out var papel);

            var pessoa = await pessoas.ObterPorSubAsync(request.Usuario, cancellationToken);
            if (pessoa is null)
                return PapelErros.PessoaDesconhecida;

            if (await papeis.ExisteAsync(id.Value, pessoa.Sub, papel, cancellationToken))
                return PapelErros.Duplicado;

            var entidade = PapelAplicacaoEntity.Criar(id.Value, pessoa.Sub, papel);
            await papeis.AdicionarAsync(entidade, cancellationToken);
            auditoria.Registrar("PapelAplicacao", entidade.PublicId, AcaoAuditoria.Criou, null, PapelErros.Estado(entidade), new DadosDoRegistro(id.Value, CategoriaAuditoria.Papeis));

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (PapelErros.DaViolacao(violacao) is not null)
            {
                return Result<PapelResumoDto>.Falha(PapelErros.DaViolacao(violacao)!);
            }

            return Result<PapelResumoDto>.Sucesso(new PapelResumoDto
            {
                Id = entidade.PublicId,
                Usuario = entidade.Usuario,
                Nome = pessoa.Nome,
                Email = pessoa.Email,
                Papel = EnumKebab.Texto(entidade.Papel)
            });
        }
    }

    public sealed class DesvincularPapelHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IPapelRepositorio papeis,
        IAuditoria auditoria) : IRequestHandler<DesvincularPapelCommand, Result>
    {
        public async Task<Result> Handle(DesvincularPapelCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return Result.Falha(AutorizacaoAplicacao.NaoEncontrada);

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.GerenciarPapeis, cancellationToken);
            if (!autorizado.IsSuccess)
                return autorizado;

            var papel = await papeis.ObterAsync(id.Value, request.PapelId, cancellationToken);
            if (papel is null)
                return Result.Falha(PapelErros.NaoEncontrado);

            auditoria.Registrar("PapelAplicacao", papel.PublicId, AcaoAuditoria.Removeu, PapelErros.Estado(papel), null, new DadosDoRegistro(id.Value, CategoriaAuditoria.Papeis));
            papeis.Remover(papel);
            return Result.Sucesso();
        }
    }
}
