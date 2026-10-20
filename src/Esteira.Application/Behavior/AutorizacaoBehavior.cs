using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Exceptions;
using MediatR;

namespace Esteira.Application.Behavios
{
    public class AutorizacaoBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private const int Proibido = 403;

        private readonly IUsuarioAtual _usuarioAtual;
        private readonly IFluxoDeployQueryRepository _queryRepository;

        public AutorizacaoBehavior(IUsuarioAtual usuarioAtual, IFluxoDeployQueryRepository queryRepository)
        {
            _usuarioAtual = usuarioAtual;
            _queryRepository = queryRepository;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is IAlteraFluxoDeploy alteracao)
            {
                var criador = await _queryRepository.ObterCriadorAsync(alteracao.IdFluxoDeploy, cancellationToken);

                if (criador is not null && !string.Equals(criador, _usuarioAtual.Usuario, StringComparison.OrdinalIgnoreCase))
                    throw new DomainException("Somente o criador do fluxo de deploy pode alterá-lo.", Proibido);
            }

            return await next();
        }
    }
}
