namespace Esteira.Application.Sessao
{
    // O retorno do login é só um caminho relativo da mesma origem ("/algo"). Qualquer outra forma
    // (host externo, esquema, "//", "/\", caractere de controle) vira "/": nunca redireciona para fora.
    public static class ReturnUrlSeguro
    {
        public const string Raiz = "/";
        public const int TamanhoMaximo = 512;

        public static string Normalizar(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.Length > TamanhoMaximo || returnUrl[0] != '/')
                return Raiz;

            if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == (char)92))
                return Raiz;

            if (returnUrl.Any(char.IsControl))
                return Raiz;

            return returnUrl;
        }
    }
}
