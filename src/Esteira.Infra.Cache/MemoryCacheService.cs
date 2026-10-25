using System.Collections.Concurrent;
using Esteira.Application.Interfaces.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace Esteira.Infra.Cache
{
    public sealed class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, byte> _chaves = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _travas = new();

        public MemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<T> ObterOuCriarAsync<T>(string chave, Func<Task<T>> fabrica, TimeSpan duracao, Func<T, bool>? deveGuardar = null)
        {
            if (_cache.TryGetValue(chave, out T valor))
                return valor;

            var trava = _travas.GetOrAdd(chave, _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync();

            try
            {
                if (_cache.TryGetValue(chave, out valor))
                    return valor;

                valor = await fabrica();

                if (deveGuardar is not null && !deveGuardar(valor))
                    return valor;

                var opcoes = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = duracao };
                opcoes.RegisterPostEvictionCallback((chaveRemovida, _, _, _) => _chaves.TryRemove((string)chaveRemovida, out _));

                _cache.Set(chave, valor, opcoes);
                _chaves[chave] = 0;

                return valor;
            }
            finally
            {
                trava.Release();
            }
        }

        public void Remover(string chave)
        {
            _cache.Remove(chave);
            _chaves.TryRemove(chave, out _);
        }

        public void RemoverPorPrefixo(string prefixo)
        {
            foreach (var chave in _chaves.Keys.Where(c => c.StartsWith(prefixo, StringComparison.Ordinal)))
                Remover(chave);
        }
    }
}
