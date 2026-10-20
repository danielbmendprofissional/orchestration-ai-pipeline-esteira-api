using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Common.Persistence.Converters
{
    public static class ListaTextoExtensions
    {
        public static PropertyBuilder<List<string>> ComoListaTexto(this PropertyBuilder<List<string>> property, string coluna, int? tamanhoMaximo = null)
        {
            property
                .HasColumnName(coluna)
                .IsRequired(false)
                .HasConversion(
                    v => v == null ? null : string.Join(',', v),
                    v => v == null ? null : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    new ValueComparer<List<string>>(
                        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                        v => v == null ? 0 : v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                        v => v == null ? null : v.ToList()));

            if (tamanhoMaximo.HasValue)
                property.HasMaxLength(tamanhoMaximo.Value);

            return property;
        }
    }
}
