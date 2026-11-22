using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using MediatR;

namespace Esteira.Application.Aplicacoes.Queries.ObterPermissoes
{
    public sealed record ObterPermissoesAplicacaoQuery(Guid AplicacaoPublicId) : IRequest<Result<PermissoesAplicacao>>;

    public sealed class ObterPermissoesAplicacaoHandler(IAcessoAplicacaoQuery acesso, IAutorizacaoAplicacao autorizacao)
        : IRequestHandler<ObterPermissoesAplicacaoQuery, Result<PermissoesAplicacao>>
    {
        public async Task<Result<PermissoesAplicacao>> Handle(ObterPermissoesAplicacaoQuery request, CancellationToken cancellationToken)
        {
            var id = await acesso.ResolverIdAsync(request.AplicacaoPublicId, cancellationToken);

            // Inexistente e sem acesso respondem igual (404), para não revelar existência.
            if (id is null)
                return Result<PermissoesAplicacao>.Falha(AutorizacaoAplicacao.NaoEncontrada);

            return await autorizacao.PermissoesAsync(id.Value, cancellationToken);
        }
    }
}
