using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Esteira.Data.Common.Persistence.Converters
{
    public sealed class SimNaoConverter : BoolToStringConverter
    {
        public SimNaoConverter() : base("N", "S")
        {
        }
    }
}
