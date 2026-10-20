namespace Esteira.Domain.Common.ValueObjects
{
    public sealed class IntValueObject
    {
        public static int NormalizeVersaoAplicacao(string value) => int.Parse(value.TrimStart('v', 'V'));

    }
}