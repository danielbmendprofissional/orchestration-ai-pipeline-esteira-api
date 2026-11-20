namespace Esteira.Domain.Common.Resultados
{
    // Result sem valor: o retorno de um command que não devolve nada pra quem chamou.
    // IsSuccess/Errors moram aqui pra que o consumidor trate qualquer Result pela base.
    public class Result
    {
        private static readonly IReadOnlyList<Erro> NenhumErro = [];

        protected Result(IReadOnlyList<Erro> erros)
        {
            Errors = erros;
        }

        public bool IsSuccess => Errors.Count == 0;

        public IReadOnlyList<Erro> Errors { get; }

        public static Result Sucesso() => new(NenhumErro);

        // Falhar sem erro é erro de programação: o contrato exige pelo menos um motivo.
        public static Result Falha(params Erro[] erros) => new(ExigirErros(erros));

        protected static IReadOnlyList<Erro> ExigirErros(Erro[]? erros)
        {
            if (erros is null || erros.Length == 0)
                throw new ArgumentException("Uma falha precisa de pelo menos um erro.", nameof(erros));

            return erros;
        }
    }
}
