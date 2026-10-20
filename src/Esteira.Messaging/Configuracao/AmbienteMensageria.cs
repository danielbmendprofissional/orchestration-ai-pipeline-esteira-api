namespace Esteira.Messaging.Configuracao
{
    public static class AmbienteMensageria
    {
        public const string Homologacao = "hml";
        public const string Producao = "prd";

        public static string Resolver(string ambienteConfigurado, bool producao)
        {
            if (!string.IsNullOrWhiteSpace(ambienteConfigurado))
                return ambienteConfigurado.Trim().ToLowerInvariant();

            return producao ? Producao : Homologacao;
        }
    }
}
