namespace Esteira.Domain.Common.Resultados
{
    // Code estável (<area>.<motivo>, ex.: "url.invalida") pro front e pros testes; Message é o texto
    // pt-BR que aparece pro usuário. Os dois são obrigatórios — erro sem code não é contrato.
    public sealed record Erro
    {
        public Erro(string code, string message, TipoErro type)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Todo erro precisa de um code estável.", nameof(code));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Todo erro precisa de uma mensagem para o usuário.", nameof(message));

            Code = code;
            Message = message;
            Type = type;
        }

        public string Code { get; }

        public string Message { get; }

        public TipoErro Type { get; }
    }
}
