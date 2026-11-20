using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Esteira.Integration.Tests.Http
{
    // Sink em memória: guarda os eventos que a API escreveu pelo Serilog, para o teste conferir o log.
    public sealed class LogDeTeste : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _eventos = new();

        public IReadOnlyCollection<LogEvent> Eventos => _eventos.ToArray();

        public void Emit(LogEvent logEvent) => _eventos.Enqueue(logEvent);
    }
}
