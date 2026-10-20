using Esteira.Application.Interfaces.Cache;
using MediatR;

namespace Esteira.Application.Behavios
{
    public class CacheBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ICacheService _cache;

        public CacheBehavior(ICacheService cache)
        {
            _cache = cache;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is ICacheableQuery consulta)
                return await _cache.ObterOuCriarAsync(consulta.ChaveCache, () => next(), consulta.DuracaoCache);

            var resposta = await next();

            if (request is IInvalidaCache invalidacao)
            {
                foreach (var prefixo in invalidacao.PrefixosCache)
                    _cache.RemoverPorPrefixo(prefixo);
            }

            return resposta;
        }
    }
}
