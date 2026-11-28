using Esteira.Application.Aplicacoes.Commands.DefinirTetoCusto;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.Obter
{
    public sealed record ObterAplicacaoQuery(Guid Id) : IRequest<Result<AplicacaoDetalheDto>>;

    public sealed class ObterAplicacaoHandler(IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao, IAplicacaoQuery query, IOpcoesDosAgentes opcoes)
        : IRequestHandler<ObterAplicacaoQuery, Result<AplicacaoDetalheDto>>
    {
        public async Task<Result<AplicacaoDetalheDto>> Handle(ObterAplicacaoQuery request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.Id, cancellationToken);
            if (id is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            var autorizado = await autorizacao.AutorizarAsync(id.Value, OperacaoAplicacao.Ver, cancellationToken);
            if (!autorizado.IsSuccess)
                return Result<AplicacaoDetalheDto>.Falha(autorizado.Errors.ToArray());

            var detalhe = await query.ObterDetalheAsync(id.Value, cancellationToken);
            if (detalhe is null)
                return AutorizacaoAplicacao.NaoEncontrada;

            // A query devolve só o teto da Aplicação; o padrão da Esteira vem da configuração a cada uso.
            var padrao = opcoes.TetoCustoPadraoUsd;
            detalhe.TetoCusto = new TetoCustoDto { TetoCustoUsd = detalhe.TetoCusto.TetoCustoUsd, PadraoUsd = padrao, EfetivoUsd = detalhe.TetoCusto.TetoCustoUsd ?? padrao };
            return Result<AplicacaoDetalheDto>.Sucesso(detalhe);
        }
    }
}
