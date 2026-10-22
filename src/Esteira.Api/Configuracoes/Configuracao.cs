namespace Esteira.Api.Configuracoes
{
    // Chaves do contrato da infra (docs/agents/infra-local.md): sem valor padrão,
    // a API não sobe apontando pra lugar errado.
    public static class Configuracao
    {
        public static string Obrigatoria(IConfiguration configuration, string chave)
        {
            var valor = configuration[chave];

            if (string.IsNullOrWhiteSpace(valor))
                throw new InvalidOperationException($"Configuração obrigatória ausente: '{chave}'. Em dev ela vem do user-secrets (infra-config.mjs secrets esteira).");

            return valor;
        }

        // "chave=valor,chave2=valor2" (formato do OTEL_RESOURCE_ATTRIBUTES)
        public static Dictionary<string, object> AtributosOtel(string? atributos)
            => (atributos ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(par => par.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(par => par.Length == 2 && par[0].Length > 0)
                .ToDictionary(par => par[0], par => (object)par[1]);
    }
}
