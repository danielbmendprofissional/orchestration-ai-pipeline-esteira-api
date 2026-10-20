namespace Esteira.Data.Common.Persistence.Migracoes
{
    internal static class ViewResumo
    {
        private const string Recurso = "Esteira.Data.Scripts.VW_FLUXO_DEPLOY_RESUMO.sql";

        public const string Remover = "DROP VIEW IF EXISTS \"VW_FLUXO_DEPLOY_RESUMO\";";

        public static string Criar()
        {
            using var stream = typeof(ViewResumo).Assembly.GetManifestResourceStream(Recurso)
                ?? throw new InvalidOperationException($"Script da view não encontrado: {Recurso}");
            using var leitor = new StreamReader(stream);
            return leitor.ReadToEnd();
        }
    }
}
