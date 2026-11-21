using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Esteira.Data.Common.Persistence.Converters
{
    // Enum do domínio <-> texto em kebab-case no banco (AprovadorTecnico <-> "aprovador-tecnico"),
    // o mesmo vocabulário da spec e do contrato. Sem número mágico na coluna.
    public sealed class EnumKebabConverter<TEnum>() : ValueConverter<TEnum, string>(v => Texto(v), v => Valor(v))
        where TEnum : struct, Enum
    {
        public static string Texto(TEnum valor)
        {
            var nome = valor.ToString();
            var sb = new StringBuilder(nome.Length + 2);
            for (var i = 0; i < nome.Length; i++)
            {
                if (char.IsUpper(nome[i]) && i > 0)
                    sb.Append('-');
                sb.Append(char.ToLowerInvariant(nome[i]));
            }
            return sb.ToString();
        }

        public static TEnum Valor(string texto)
        {
            foreach (var candidato in Enum.GetValues<TEnum>())
                if (Texto(candidato) == texto)
                    return candidato;

            throw new InvalidOperationException($"Valor '{texto}' desconhecido para {typeof(TEnum).Name}.");
        }
    }
}
