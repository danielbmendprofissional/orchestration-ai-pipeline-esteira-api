using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Commands.DefinirTetoCusto
{
    // `tetoCustoUsd` nulo na resposta = a Aplicação usa o padrão da Esteira.
    public sealed class TetoCustoDto
    {
        public decimal? TetoCustoUsd { get; set; }
        public decimal PadraoUsd { get; set; }
        public decimal EfetivoUsd { get; set; }
    }

    // TetoCustoUsd nulo restaura o padrão da Esteira.
    public sealed record DefinirTetoCustoCommand(Guid AplicacaoId, decimal? TetoCustoUsd) : IRequest<Result<TetoCustoDto>>, ITransactionRequest;

    public sealed class DefinirTetoCustoHandler(
        IAcessoAplicacaoQuery acesso,
        IAutorizacaoAplicacao autorizacao,
        IAplicacaoRepositorio repositorio,
        IUnitOfWork uow,
        IAuditoria auditoria,
        IOpcoesDosAgentes opcoes) : IRequestHandler<DefinirTetoCustoCommand, Result<TetoCustoDto>>
    {
        public static readonly Erro ConflitoDeGravacao = new("aplicacao.conflito-de-gravacao", "Outra alteração foi gravada ao mesmo tempo nesta Aplicação; recarregue e tente de novo.", TipoErro.Conflict);

        public async Task<Result<TetoCustoDto>> Handle(DefinirTetoCustoCommand request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoId, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.EditarTetoCusto, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<TetoCustoDto>.Falha(autorizado.Errors.ToArray());

            var aplicacao = await repositorio.ObterAsync(id.Value, cancellationToken);
            if (aplicacao is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var antes = Estado(aplicacao);
            var definido = aplicacao.DefinirTetoCusto(request.TetoCustoUsd);
            if (!definido.IsSuccess)
                return Result<TetoCustoDto>.Falha(definido.Errors.ToArray());

            auditoria.Registrar("Aplicacao", aplicacao.PublicId, AcaoAuditoria.Alterou, antes, Estado(aplicacao));

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ConcorrenciaOtimistaException)
            {
                // Perdeu a corrida: o que o vencedor gravou decide. Mesmo valor = idempotente; outro = 409.
                var gravado = await repositorio.ObterTetoCustoGravadoAsync(id.Value, cancellationToken);
                return gravado == request.TetoCustoUsd
                    ? Result<TetoCustoDto>.Sucesso(Dto(gravado))
                    : Result<TetoCustoDto>.Falha(ConflitoDeGravacao);
            }

            return Result<TetoCustoDto>.Sucesso(Dto(aplicacao.TetoCustoAgentesUsd));
        }

        private TetoCustoDto Dto(decimal? teto)
        {
            var padrao = opcoes.TetoCustoPadraoUsd;
            return new TetoCustoDto { TetoCustoUsd = teto, PadraoUsd = padrao, EfetivoUsd = teto ?? padrao };
        }

        private static object Estado(AplicacaoEntity a) => new { a.TetoCustoAgentesUsd };
    }
}
