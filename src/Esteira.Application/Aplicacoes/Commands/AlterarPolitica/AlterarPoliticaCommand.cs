using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.AlterarPolitica
{
    // Limite null = sem limite. Ambiente e Modo chegam em texto kebab-case (`qa`, `bloqueia`).
    public sealed record AlterarPoliticaCommand(Guid AplicacaoId, string Ambiente, int? Critica, int? Alta, int? Media, int? Baixa, string Modo)
        : IRequest<Result<PoliticaDto>>, ITransactionRequest;

    public sealed class AlterarPoliticaValidator : AbstractValidator<AlterarPoliticaCommand>
    {
        public AlterarPoliticaValidator()
        {
            RuleFor(x => x.Ambiente)
                .Must(a => EnumKebab.TentarLer<AmbientePolitica>(a, out _))
                .WithErrorCode("politica.ambiente-invalido")
                .WithMessage("Ambiente inválido. Use review, qa, hml ou producao.");
            RuleFor(x => x.Modo)
                .Must(m => EnumKebab.TentarLer<ModoPolitica>(m, out _))
                .WithErrorCode("politica.modo-invalido")
                .WithMessage("Modo inválido. Use avisa ou bloqueia.");
        }
    }

    public sealed class AlterarPoliticaHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio repositorio,
        IAuditoria auditoria) : IRequestHandler<AlterarPoliticaCommand, Result<PoliticaDto>>
    {
        public async Task<Result<PoliticaDto>> Handle(AlterarPoliticaCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.EditarPolitica, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<PoliticaDto>.Falha(autorizado.Errors.ToArray());

            EnumKebab.TentarLer<AmbientePolitica>(request.Ambiente, out var ambiente);
            EnumKebab.TentarLer<ModoPolitica>(request.Modo, out var modo);

            var politica = await repositorio.ObterPoliticaAsync(id.Value, ambiente, cancellationToken);
            if (politica is null)
                return AplicacaoErros.PoliticaNaoEncontrada;

            var antes = AplicacaoErros.Dto(politica);
            var alterada = politica.Alterar(request.Critica, request.Alta, request.Media, request.Baixa, modo);
            if (!alterada.IsSuccess)
                return Result<PoliticaDto>.Falha(alterada.Errors.ToArray());

            var depois = AplicacaoErros.Dto(politica);
            auditoria.Registrar("PoliticaSeveridade", politica.PublicId, AcaoAuditoria.Alterou, antes, depois, new DadosDoRegistro(politica.AplicacaoId, CategoriaAuditoria.Politica));

            return Result<PoliticaDto>.Sucesso(depois);
        }
    }
}
