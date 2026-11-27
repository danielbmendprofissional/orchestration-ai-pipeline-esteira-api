using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Eventos
{
    // Um stream SSE aberto: recebe só os eventos das Aplicações com que foi aberto. Descartar libera a inscrição.
    public interface IAssinanteDeEventos : IDisposable
    {
        ChannelReader<EventoDeTempoReal> Eventos { get; }
    }

    // Fan-out em memória de UMA instância: a assinatura do Redis chama `Distribuir`; cada stream aberto chama `Assinar`.
    public interface IDistribuidorDeEventos
    {
        IAssinanteDeEventos Assinar(IReadOnlySet<Guid> aplicacoesVisiveis);

        void Distribuir(EventoDeTempoReal evento);
    }

    public sealed class DistribuidorDeEventos(ILogger<DistribuidorDeEventos> log) : IDistribuidorDeEventos
    {
        // Um stream lento não pode segurar memória: passando disto, o evento mais antigo cai (o front recarrega ao reconectar).
        private const int CapacidadePorStream = 256;

        private readonly ConcurrentDictionary<Guid, Assinante> _assinantes = new();

        public int Conectados => _assinantes.Count;

        public IAssinanteDeEventos Assinar(IReadOnlySet<Guid> aplicacoesVisiveis)
        {
            var assinante = new Assinante(aplicacoesVisiveis, a => _assinantes.TryRemove(a.Id, out _));
            _assinantes[assinante.Id] = assinante;
            return assinante;
        }

        public void Distribuir(EventoDeTempoReal evento)
        {
            var entregues = 0;
            foreach (var assinante in _assinantes.Values)
                if (assinante.Aplicacoes.Contains(evento.AplicacaoId) && assinante.Escritor.TryWrite(evento))
                    entregues++;

            if (entregues == 0)
                log.LogDebug("Evento {Tipo} sem nenhum stream interessado nesta instância", evento.Tipo);
        }

        private sealed class Assinante : IAssinanteDeEventos
        {
            private readonly Channel<EventoDeTempoReal> _canal = Channel.CreateBounded<EventoDeTempoReal>(
                new BoundedChannelOptions(CapacidadePorStream)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });

            private readonly Action<Assinante> _aoDescartar;

            public Assinante(IReadOnlySet<Guid> aplicacoes, Action<Assinante> aoDescartar)
            {
                Aplicacoes = aplicacoes;
                _aoDescartar = aoDescartar;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public IReadOnlySet<Guid> Aplicacoes { get; }
            public ChannelWriter<EventoDeTempoReal> Escritor => _canal.Writer;
            public ChannelReader<EventoDeTempoReal> Eventos => _canal.Reader;

            public void Dispose()
            {
                _aoDescartar(this);
                _canal.Writer.TryComplete();
            }
        }
    }
}
