using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Esteira.Api.Erros
{
    // [ApiController] recusa o corpo antes do handler (JSON malformado, tipo errado). A resposta segue
    // o mesmo contrato da ADR-0003: uma entrada por campo com erro, nunca o ValidationProblemDetails padrão.
    // O ModelState registra também uma chave pro próprio parâmetro [FromBody] ("pedido") quando o binding
    // falha; ela não é um campo e é descartada. Sem nenhum campo identificável, sai uma única entrada.
    public static class RequisicaoInvalida
    {
        private const string Code = "requisicao.invalida";
        private const string Message = "A requisição é inválida.";

        public static IActionResult Resposta(ActionContext context)
        {
            var erros = CamposInvalidos(context)
                .Select(_ => new Erro(Code, Message, TipoErro.Validation))
                .DefaultIfEmpty(new Erro(Code, Message, TipoErro.Validation))
                .ToArray();

            return new BadRequestObjectResult(RespostaDeErro.De(erros));
        }

        // Uma entrada por campo: chaves que apontam pro mesmo caminho JSON viram um único campo.
        private static IEnumerable<string> CamposInvalidos(ActionContext context)
        {
            var parametrosDoCorpo = context.ActionDescriptor.Parameters
                .Where(parametro => parametro.BindingInfo?.BindingSource == BindingSource.Body)
                .Select(parametro => parametro.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return context.ModelState
                .Where(campo => campo.Value?.Errors.Count > 0)
                .Select(campo => Normalizar(campo.Key, parametrosDoCorpo))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(campo => campo, StringComparer.Ordinal);
        }

        private static string? Normalizar(string chave, HashSet<string> parametrosDoCorpo)
        {
            var caminho = chave.Trim();
            if (caminho.Length == 0 || caminho == "$")
                return null; // erro do documento inteiro, sem campo identificável

            foreach (var parametro in parametrosDoCorpo)
            {
                if (caminho.Equals(parametro, StringComparison.OrdinalIgnoreCase))
                    return null; // o próprio parâmetro do corpo, não um campo

                var prefixo = parametro + ".";
                if (caminho.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                {
                    caminho = caminho[prefixo.Length..];
                    break;
                }
            }

            if (caminho.StartsWith("$.", StringComparison.Ordinal))
                caminho = caminho[2..];

            if (caminho.StartsWith('$'))
                caminho = caminho[1..].TrimStart('.');

            return caminho.Length == 0 ? null : caminho;
        }
    }
}
