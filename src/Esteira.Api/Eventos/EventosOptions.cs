namespace Esteira.Api.Eventos
{
    // Limites do SSE (T1.2). Os padrões são os da spec; testes trocam por valores curtos.
    public sealed class EventosOptions
    {
        public TimeSpan IntervaloDePing { get; init; } = TimeSpan.FromSeconds(15);

        public TimeSpan DuracaoMaxima { get; init; } = TimeSpan.FromMinutes(5);

        public int MaximoDeConexoesPorPessoa { get; init; } = 6;
    }
}
