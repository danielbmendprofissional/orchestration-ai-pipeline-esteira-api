using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Esteira.Domain.Common.ValueObjects
{
    public sealed class StringValueObject
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            // Remove espaços extras
            value = value.Trim();

            // Remove acentos
            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);

                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            value = sb.ToString().Normalize(NormalizationForm.FormC);

            // Remove caracteres especiais
            value = Regex.Replace(value, @"[^a-zA-Z0-9\s_]", "");


            return value.ToUpper();
        }
       
    }
}
