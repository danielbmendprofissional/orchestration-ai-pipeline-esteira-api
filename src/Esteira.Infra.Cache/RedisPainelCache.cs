using Esteira.Application.Fatias;
using StackExchange.Redis;

namespace Esteira.Infra.Cache
{
    // Guardado do Painel de Fatias no Redis do projeto. Só GET/SET (com NX e TTL)/DEL, toda chave com `Redis:KeyPrefix`
    // (a ACL nega SCAN/KEYS e scripts).
    public sealed class RedisPainelCache(IConnectionMultiplexer redis, string prefixo) : IPainelCache
    {
        private IDatabase Banco => redis.GetDatabase();

        private string Chave(string chave) => $"{prefixo}{chave}";

        public async Task<string?> ObterAsync(string chave, CancellationToken cancellationToken = default)
        {
            var valor = await Banco.StringGetAsync(Chave(chave));
            return valor.IsNullOrEmpty ? null : (string?)valor;
        }

        public Task GuardarAsync(string chave, string valor, TimeSpan validade, CancellationToken cancellationToken = default)
            => Banco.StringSetAsync(Chave(chave), valor, validade);

        public Task<bool> TentarGuardarSeAusenteAsync(string chave, string valor, TimeSpan validade, CancellationToken cancellationToken = default)
            => Banco.StringSetAsync(Chave(chave), valor, validade, When.NotExists);

        public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
            => Banco.KeyDeleteAsync(Chave(chave));

        // GET + DEL (a ACL nega script); a janela é a do TTL da trava.
        public async Task RemoverSeValorAsync(string chave, string valor, CancellationToken cancellationToken = default)
        {
            if ((string?)await Banco.StringGetAsync(Chave(chave)) == valor)
                await Banco.KeyDeleteAsync(Chave(chave));
        }
    }
}
