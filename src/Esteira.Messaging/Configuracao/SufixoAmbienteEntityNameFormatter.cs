using MassTransit;

namespace Esteira.Messaging.Configuracao
{
    public sealed class SufixoAmbienteEntityNameFormatter : IEntityNameFormatter
    {
        private readonly IEntityNameFormatter _formatter;
        private readonly string _sufixo;

        public SufixoAmbienteEntityNameFormatter(IEntityNameFormatter formatter, string ambiente)
        {
            _formatter = formatter;
            _sufixo = $"-{ambiente}";
        }

        public string FormatEntityName<T>() => _formatter.FormatEntityName<T>() + _sufixo;
    }
}
