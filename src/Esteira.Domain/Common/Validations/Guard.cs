namespace Esteira.Domain.Common.Validations
{
    // Só erro de programação (argumento nulo, enum fora do intervalo): vira 500.
    // Entrada inválida de usuário é Result de falha, nunca Guard.
    internal static class Guard
    {
        public static void AgainstNull<T>(T value, string paramName)
        {
            if (value == null)
                throw new ArgumentNullException(paramName, $"{paramName} não pode ser nulo");
        }

        public static void AgainstNullOrWhiteSpace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName} não pode ser nulo ou vazio", paramName);
        }

        public static void AgainstInvalidEnum<TEnum>(object value, string paramName) where TEnum : Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
                throw new ArgumentException($"parâmetro {paramName} inválido", paramName);
        }
    }
}
