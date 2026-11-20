namespace Esteira.Domain.Common.Resultados
{
    // Result com valor: só sucesso tem Value. Ler Value de uma falha é bug de quem chamou,
    // por isso lança em vez de devolver default.
    public sealed class Result<T> : Result
    {
        private readonly T _valor;

        private Result(T valor) : base([])
        {
            _valor = valor;
        }

        private Result(IReadOnlyList<Erro> erros) : base(erros)
        {
            _valor = default!;
        }

        public T Value => IsSuccess
            ? _valor
            : throw new InvalidOperationException("Não há valor em um resultado de falha.");

        public static Result<T> Sucesso(T valor) => new(valor);

        public new static Result<T> Falha(params Erro[] erros) => new(ExigirErros(erros));

        // Conversão implícita pro envelope sem valor: um handler que devolve Result<T> pode ser
        // tratado como Result pela base sem cast.
        public static implicit operator Result<T>(Erro erro) => Falha(erro);
    }
}
