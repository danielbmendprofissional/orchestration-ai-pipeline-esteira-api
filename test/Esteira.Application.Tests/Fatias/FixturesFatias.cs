namespace Esteira.Application.Tests.Fatias
{
    internal static class FixturesFatias
    {
        public static string Ler(string nome) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fatias", nome));
    }
}
