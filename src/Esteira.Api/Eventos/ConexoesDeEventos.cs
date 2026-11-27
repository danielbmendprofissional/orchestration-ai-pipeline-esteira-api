using System.Collections.Concurrent;

namespace Esteira.Api.Eventos
{
    // Conexões SSE abertas por pessoa NESTA instância (o limite de 6 vale por instância: contar entre instâncias pediria
    // um contador no Redis, que a spec não pede).
    public sealed class ConexoesDeEventos
    {
        private readonly ConcurrentDictionary<string, int> _porPessoa = new();

        // null = a pessoa já está no limite. O retorno libera a vaga ao ser descartado (idempotente).
        public IDisposable? TentarAbrir(string pessoa, int maximo)
        {
            var aceita = false;
            _porPessoa.AddOrUpdate(pessoa,
                _ => { aceita = maximo > 0; return aceita ? 1 : 0; },
                (_, atual) => { aceita = atual < maximo; return aceita ? atual + 1 : atual; });

            return aceita ? new Vaga(this, pessoa) : null;
        }

        private void Liberar(string pessoa)
        {
            var atual = _porPessoa.AddOrUpdate(pessoa, 0, (_, n) => Math.Max(0, n - 1));
            if (atual == 0)
                _porPessoa.TryRemove(new KeyValuePair<string, int>(pessoa, 0));
        }

        private sealed class Vaga(ConexoesDeEventos dono, string pessoa) : IDisposable
        {
            private int _liberada;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _liberada, 1) == 0)
                    dono.Liberar(pessoa);
            }
        }
    }
}
