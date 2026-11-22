using System.Text;

namespace Esteira.Application.Common.Extensions
{
    // Vocabulário da spec em texto (AprovadorTecnico <-> "aprovador-tecnico"); mesma regra do EnumKebabConverter da Data.
    public static class EnumKebab
    {
        public static string Texto<TEnum>(TEnum valor) where TEnum : struct, Enum
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

        public static bool TentarLer<TEnum>(string? texto, out TEnum valor) where TEnum : struct, Enum
        {
            foreach (var candidato in Enum.GetValues<TEnum>())
                if (Texto(candidato) == texto)
                {
                    valor = candidato;
                    return true;
                }

            valor = default;
            return false;
        }
    }
}
