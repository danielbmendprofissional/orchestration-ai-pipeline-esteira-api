using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.Destinos
{
    // Corpo `{ nome, configuracao, ordem }`. Configuração e ordem são nullable de propósito (a ausência cai nos validators);
    // `ordem` ausente vale 0. A configuração aceita nunca carrega segredo (critério 10), então vai inteira para a auditoria.
    public sealed record CriarDestinoCommand(Guid ProjetoId, string Ambiente, string Nome, Dictionary<string, string>? Configuracao, int? Ordem)
        : IRequest<Result<DestinoDto>>, ITransactionRequest;

    public sealed record AlterarDestinoCommand(Guid ProjetoId, string Ambiente, Guid DestinoId, string Nome, Dictionary<string, string>? Configuracao, int? Ordem)
        : IRequest<Result<DestinoDto>>, ITransactionRequest;

    public sealed record RemoverDestinoCommand(Guid ProjetoId, string Ambiente, Guid DestinoId)
        : IRequest<Result>, ITransactionRequest;

    internal static class DestinoRegras
    {
        public static IRuleBuilderOptions<T, string> AmbienteValido<T>(this IRuleBuilder<T, string> regra)
            => regra.Must(a => EnumKebab.TentarLer<AmbienteDeploy>(a, out _))
                .WithErrorCode("ambiente.invalido")
                .WithMessage("Ambiente inválido. Use qa, hml ou producao.");

        public static IRuleBuilderOptions<T, Dictionary<string, string>?> ConfiguracaoObrigatoria<T>(this IRuleBuilder<T, Dictionary<string, string>?> regra)
            => regra.NotNull().WithErrorCode("destino.configuracao-obrigatoria").WithMessage("Informe a configuração do Destino (pode ser vazia).");

        public static object Estado(ProjetoEntity projeto, DestinoEntity d) => new
        {
            Projeto = projeto.PublicId,
            Ambiente = EnumKebab.Texto(d.Ambiente),
            d.Nome,
            Configuracao = new Dictionary<string, string>(d.Configuracao),
            d.Ordem
        };
    }

    public sealed class CriarDestinoValidator : AbstractValidator<CriarDestinoCommand>
    {
        public CriarDestinoValidator()
        {
            RuleFor(x => x.Ambiente).AmbienteValido();
            RuleFor(x => x.Configuracao).ConfiguracaoObrigatoria();
        }
    }

    public sealed class AlterarDestinoValidator : AbstractValidator<AlterarDestinoCommand>
    {
        public AlterarDestinoValidator()
        {
            RuleFor(x => x.Ambiente).AmbienteValido();
            RuleFor(x => x.Configuracao).ConfiguracaoObrigatoria();
        }
    }

    public sealed class RemoverDestinoValidator : AbstractValidator<RemoverDestinoCommand>
    {
        public RemoverDestinoValidator() => RuleFor(x => x.Ambiente).AmbienteValido();
    }

    public sealed class CriarDestinoHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IUnitOfWork uow, IAuditoria auditoria)
        : IRequestHandler<CriarDestinoCommand, Result<DestinoDto>>
    {
        public async Task<Result<DestinoDto>> Handle(CriarDestinoCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result<DestinoDto>.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            EnumKebab.TentarLer<AmbienteDeploy>(request.Ambiente, out var ambiente);

            var linha = await projetos.ObterAmbienteAsync(projeto.Id, ambiente, cancellationToken);
            if (linha is null)
                return ProjetoErros.AmbienteNaoEncontrado;
            if (!linha.Habilitado)
                return ProjetoErros.AmbienteDesabilitado;

            var criado = DestinoEntity.Criar(projeto.Id, ambiente, request.Nome ?? string.Empty, request.Configuracao!, request.Ordem ?? 0);
            if (!criado.IsSuccess)
                return Result<DestinoDto>.Falha(criado.Errors.ToArray());

            var destino = criado.Value;
            if (await projetos.DestinoNomeExisteAsync(projeto.Id, ambiente, destino.Nome, null, cancellationToken))
                return ProjetoErros.DestinoNomeDuplicado;

            await projetos.AdicionarDestinoAsync(destino, cancellationToken);
            auditoria.Registrar("Destino", destino.PublicId, AcaoAuditoria.Criou, null, DestinoRegras.Estado(projeto, destino), new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (ProjetoErros.DaViolacao(violacao) is not null)
            {
                return Result<DestinoDto>.Falha(ProjetoErros.DaViolacao(violacao)!);
            }

            return Result<DestinoDto>.Sucesso(ProjetoErros.Dto(destino));
        }
    }

    public sealed class AlterarDestinoHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IUnitOfWork uow, IAuditoria auditoria)
        : IRequestHandler<AlterarDestinoCommand, Result<DestinoDto>>
    {
        public async Task<Result<DestinoDto>> Handle(AlterarDestinoCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result<DestinoDto>.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            EnumKebab.TentarLer<AmbienteDeploy>(request.Ambiente, out var ambiente);

            var destino = await projetos.ObterDestinoAsync(projeto.Id, ambiente, request.DestinoId, cancellationToken);
            if (destino is null)
                return ProjetoErros.DestinoNaoEncontrado;

            var nome = (request.Nome ?? string.Empty).Trim();
            if (nome.Length > 0 && await projetos.DestinoNomeExisteAsync(projeto.Id, ambiente, nome, destino.Id, cancellationToken))
                return ProjetoErros.DestinoNomeDuplicado;

            var antes = DestinoRegras.Estado(projeto, destino);
            var alterado = destino.Alterar(nome, request.Configuracao!, request.Ordem ?? 0);
            if (!alterado.IsSuccess)
                return Result<DestinoDto>.Falha(alterado.Errors.ToArray());

            auditoria.Registrar("Destino", destino.PublicId, AcaoAuditoria.Alterou, antes, DestinoRegras.Estado(projeto, destino), new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ViolacaoDeUnicidadeException violacao) when (ProjetoErros.DaViolacao(violacao) is not null)
            {
                return Result<DestinoDto>.Falha(ProjetoErros.DaViolacao(violacao)!);
            }

            return Result<DestinoDto>.Sucesso(ProjetoErros.Dto(destino));
        }
    }

    public sealed class RemoverDestinoHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IAuditoria auditoria)
        : IRequestHandler<RemoverDestinoCommand, Result>
    {
        public async Task<Result> Handle(RemoverDestinoCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            EnumKebab.TentarLer<AmbienteDeploy>(request.Ambiente, out var ambiente);

            var destino = await projetos.ObterDestinoAsync(projeto.Id, ambiente, request.DestinoId, cancellationToken);
            if (destino is null)
                return Result.Falha(ProjetoErros.DestinoNaoEncontrado);

            auditoria.Registrar("Destino", destino.PublicId, AcaoAuditoria.Removeu, DestinoRegras.Estado(projeto, destino), null, new DadosDoRegistro(projeto.AplicacaoId, CategoriaAuditoria.Cadastro));
            projetos.RemoverDestino(destino);
            return Result.Sucesso();
        }
    }
}
