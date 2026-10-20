using MassTransit;

namespace Esteira.Messaging.Configuracao
{
    public sealed class SufixoAmbienteEndpointNameFormatter : IEndpointNameFormatter
    {
        private readonly IEndpointNameFormatter _formatter;
        private readonly string _sufixo;

        public SufixoAmbienteEndpointNameFormatter(IEndpointNameFormatter formatter, string ambiente)
        {
            _formatter = formatter;
            _sufixo = $"{formatter.Separator}{ambiente}";
        }

        public string Separator => _formatter.Separator;

        public string TemporaryEndpoint(string tag) => _formatter.TemporaryEndpoint(tag);

        public string Consumer<T>() where T : class, IConsumer => _formatter.Consumer<T>() + _sufixo;

        public string Message<T>() where T : class => _formatter.Message<T>() + _sufixo;

        public string Saga<T>() where T : class, ISaga => _formatter.Saga<T>() + _sufixo;

        public string ExecuteActivity<T, TArguments>() where T : class, IExecuteActivity<TArguments> where TArguments : class
            => _formatter.ExecuteActivity<T, TArguments>() + _sufixo;

        public string CompensateActivity<T, TLog>() where T : class, ICompensateActivity<TLog> where TLog : class
            => _formatter.CompensateActivity<T, TLog>() + _sufixo;

        public string SanitizeName(string name) => _formatter.SanitizeName(name);
    }
}
