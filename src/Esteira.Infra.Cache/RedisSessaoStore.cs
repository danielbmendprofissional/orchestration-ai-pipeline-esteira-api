using System.Security.Cryptography;
using System.Text.Json;
using Esteira.Application.Interfaces.Sessao;
using StackExchange.Redis;

namespace Esteira.Infra.Cache
{
    // Sessão BFF no Redis (ADR-0004). A ACL do projeto só deixa chaves `Redis:KeyPrefix*` e nega SCAN/KEYS e scripts,
    // então o adaptador usa só GET/SET/DEL/SADD/SREM/SMEMBERS/EXPIRE e um conjunto por usuário como índice.
    public sealed class RedisSessaoStore : ISessaoStore
    {
        private static readonly TimeSpan DuracaoDaTrava = TimeSpan.FromSeconds(10);

        private readonly IConnectionMultiplexer _redis;
        private readonly string _prefixo;

        public RedisSessaoStore(IConnectionMultiplexer redis, string prefixo)
        {
            _redis = redis;
            _prefixo = prefixo;
        }

        private IDatabase Banco => _redis.GetDatabase();

        private string ChaveSessao(string id) => $"{_prefixo}sessao:{id}";
        private string ChaveIndice(string sub) => $"{_prefixo}usuario:{sub}:sessoes";
        private string ChaveLogin(string state) => $"{_prefixo}login:{state}";
        private string ChaveTrava(string id) => $"{_prefixo}refresh:{id}";

        public async Task CriarAsync(SessaoUsuario sessao, TimeSpan duracao)
        {
            await Banco.StringSetAsync(ChaveSessao(sessao.Id), JsonSerializer.Serialize(sessao), duracao);
            await IndexarAsync(sessao, duracao);
        }

        // O índice só estende o TTL: uma sessão de vida curta não pode encurtar o índice de outra, mais longa,
        // senão a revogação geral deixaria essa outra viva.
        private async Task IndexarAsync(SessaoUsuario sessao, TimeSpan duracao)
        {
            var indice = ChaveIndice(sessao.Sub);
            await Banco.SetAddAsync(indice, sessao.Id);
            var atual = await Banco.KeyTimeToLiveAsync(indice);

            if (atual is null || atual < duracao)
                await Banco.KeyExpireAsync(indice, duracao);
        }

        public async Task<SessaoUsuario?> ObterAsync(string id)
        {
            var valor = await Banco.StringGetAsync(ChaveSessao(id));
            return valor.IsNullOrEmpty ? null : JsonSerializer.Deserialize<SessaoUsuario>((string)valor!);
        }

        // SET ... XX: se o logout ou a revogação geral apagou a sessão durante o refresh, ela não ressuscita.
        public async Task<bool> AtualizarAsync(SessaoUsuario sessao, TimeSpan duracao)
        {
            var atualizou = await Banco.StringSetAsync(ChaveSessao(sessao.Id), JsonSerializer.Serialize(sessao), duracao, When.Exists);

            if (atualizou)
                await IndexarAsync(sessao, duracao);

            return atualizou;
        }

        public async Task RevogarAsync(string id)
        {
            var sessao = await ObterAsync(id);
            await Banco.KeyDeleteAsync(ChaveSessao(id));

            if (sessao is not null)
                await Banco.SetRemoveAsync(ChaveIndice(sessao.Sub), id);
        }

        public async Task RevogarTodasDoUsuarioAsync(string sub)
        {
            var indice = ChaveIndice(sub);
            var ids = await Banco.SetMembersAsync(indice);

            foreach (var id in ids)
                await Banco.KeyDeleteAsync(ChaveSessao(id.ToString()));

            await Banco.KeyDeleteAsync(indice);
        }

        public Task GuardarLoginAsync(string state, ContextoLogin contexto, TimeSpan duracao)
            => Banco.StringSetAsync(ChaveLogin(state), JsonSerializer.Serialize(contexto), duracao);

        public async Task<ContextoLogin?> ConsumirLoginAsync(string state)
        {
            var chave = ChaveLogin(state);
            var valor = await Banco.StringGetAsync(chave);

            // O DEL decide quem consome: se duas chamadas leem o mesmo state, só a que apagou a chave segue.
            if (valor.IsNullOrEmpty || !await Banco.KeyDeleteAsync(chave))
                return null;

            return JsonSerializer.Deserialize<ContextoLogin>((string)valor!);
        }

        public async Task<string?> TentarTravarRefreshAsync(string id)
        {
            var dono = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            return await Banco.StringSetAsync(ChaveTrava(id), dono, DuracaoDaTrava, When.NotExists) ? dono : null;
        }

        // Só o dono libera: se a trava venceu e outra chamada a retomou, a primeira não pode apagar a alheia.
        // GET + DEL (a ACL nega script); a janela entre os dois é a do TTL da trava, não de uma troca inteira.
        public async Task LiberarRefreshAsync(string id, string dono)
        {
            var chave = ChaveTrava(id);

            if ((string?)await Banco.StringGetAsync(chave) == dono)
                await Banco.KeyDeleteAsync(chave);
        }
    }
}
