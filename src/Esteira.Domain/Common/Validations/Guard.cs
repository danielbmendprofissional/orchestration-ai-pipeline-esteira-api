using Esteira.Domain.Common.Exceptions;
//novo
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Domain.Common.Validations
{
    internal static class Guard
    {
        public static void AgainstNull<T>(T value, string paramName)
        {
            if (value == null)
                throw new DomainException($"{paramName} não pode ser nulo");
        }

        public static void AgainstNullOrWhiteSpace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{paramName} não pode ser nulo ou vazio");
        }

        public static void AgainstInvalidEnum<TEnum>(object value, string paramName) where TEnum : Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
                throw new DomainException($"parâmetro {paramName} inválido");
        }

        public static void Against<TException>(bool condition, string message) where TException : Exception
        {
            if (!condition) return;

            throw (TException)Activator.CreateInstance(
                typeof(TException),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
                binder: null,
                args: new object[] { message },
                culture: null)!;
        }
    }
}
