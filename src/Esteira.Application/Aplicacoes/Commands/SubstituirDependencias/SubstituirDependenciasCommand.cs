using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using FluentValidation;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.SubstituirDependencias
{
    // DependeDe é nullable de propósito: omitir o campo não pode esvaziar as dependências.
    public sealed record SubstituirDependenciasCommand(Guid ProjetoId, IReadOnlyList<Guid>? DependeDe)
        : IRequest<Result<ProjetoResumoDto>>, ITransactionRequest;

    public sealed class SubstituirDependenciasValidator : AbstractValidator<SubstituirDependenciasCommand>
    {
        public SubstituirDependenciasValidator()
        {
            RuleFor(x => x.DependeDe).NotNull().WithErrorCode("projeto.depende-de-obrigatorio").WithMessage("Informe a lista dependeDe (pode ser vazia).");
        }
    }

    public sealed class SubstituirDependenciasHandler(IAutorizacaoAplicacao autorizacao, IProjetoRepositorio projetos, IAuditoria auditoria)
        : IRequestHandler<SubstituirDependenciasCommand, Result<ProjetoResumoDto>>
    {
        public async Task<Result<ProjetoResumoDto>> Handle(SubstituirDependenciasCommand request, CancellationToken cancellationToken)
        {
            var carregado = await ProjetoErros.CarregarAutorizadoAsync(projetos, autorizacao, request.ProjetoId, cancellationToken);
            if (!carregado.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(carregado.Errors.ToArray());

            var projeto = carregado.Value;
            var daAplicacao = await projetos.ProjetosDaAplicacaoAsync(projeto.AplicacaoId, cancellationToken);
            var idPorPublicId = daAplicacao.ToDictionary(p => p.PublicId, p => p.Id);
            var publicIdPorId = daAplicacao.ToDictionary(p => p.Id, p => p.PublicId);

            // PublicId que não é de um Projeto desta Aplicação (de outra, ou inexistente) é dependência inválida.
            var pedidos = request.DependeDe!.Distinct().ToList();
            if (pedidos.Any(p => !idPorPublicId.ContainsKey(p)))
                return GrafoDeDependencias.DependenciaInvalida;

            var novos = pedidos.Select(p => idPorPublicId[p]).ToList();
            var arestas = await projetos.DependenciasDaAplicacaoAsync(projeto.AplicacaoId, cancellationToken);

            var valida = GrafoDeDependencias.Validar(
                projeto.Id, novos, daAplicacao.Select(p => p.Id).ToList(), arestas.Select(a => (a.ProjetoId, a.DependeDeProjetoId)));
            if (!valida.IsSuccess)
                return Result<ProjetoResumoDto>.Falha(valida.Errors.ToArray());

            var atuais = arestas.Where(a => a.ProjetoId == projeto.Id).ToList();
            var antes = atuais.Select(a => publicIdPorId[a.DependeDeProjetoId]).ToList();

            foreach (var saiu in atuais.Where(a => !novos.Contains(a.DependeDeProjetoId)))
                projetos.RemoverDependencia(saiu);

            foreach (var novo in novos.Where(n => atuais.All(a => a.DependeDeProjetoId != n)))
            {
                var criada = DependenciaProjetoEntity.Criar(projeto.Id, novo);
                await projetos.AdicionarDependenciaAsync(criada.Value, cancellationToken);
            }

            auditoria.Registrar("Projeto", projeto.PublicId, AcaoAuditoria.Alterou, new { DependeDe = antes }, new { DependeDe = pedidos });

            return Result<ProjetoResumoDto>.Sucesso(await projetos.MontarDtoAsync(projeto, cancellationToken));
        }
    }
}
