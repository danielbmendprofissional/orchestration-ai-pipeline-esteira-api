using System.ComponentModel;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Api.Erros
{
    // Corpo de falha: { "errors": [ { "code", "message", "type" } ] }, todos os erros na ordem.
    public sealed record ErroNaResposta(string Code, string Message, string Type)
    {
        public static ErroNaResposta De(Erro erro) => new(erro.Code, erro.Message, erro.Type.ToString());
    }

    public sealed record RespostaDeErro(IReadOnlyList<ErroNaResposta> Errors)
    {
        public static RespostaDeErro De(IReadOnlyList<Erro> erros)
            => new(erros.Select(ErroNaResposta.De).ToArray());
    }
}
