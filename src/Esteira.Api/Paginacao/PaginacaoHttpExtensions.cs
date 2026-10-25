using System.Globalization;
using Esteira.Application.Common.Paginacao;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Api.Paginacao
{
    public static class PaginacaoHttpExtensions
    {
        // ADR-0002: X-Page/X-Page-Size nos headers, sort/order na query string. Valor inválido nunca é
        // corrigido em silêncio: volta 400 com todos os erros de uma vez.
        public static Result<PaginacaoDto> ObterPaginacao(this HttpRequest request)
        {
            var erros = new List<Erro>();

            var pagina = LerInteiro(request.Headers[HeadersPaginacao.Pagina].ToString(), padrao: 1, minimo: 1, maximo: int.MaxValue);
            if (pagina is null)
                erros.Add(PaginacaoErros.PaginaInvalida());

            var tamanho = LerInteiro(request.Headers[HeadersPaginacao.TamanhoPagina].ToString(),
                padrao: PaginacaoDto.TamanhoPadrao, minimo: 1, maximo: PaginacaoDto.TamanhoMaximo);
            if (tamanho is null)
                erros.Add(PaginacaoErros.TamanhoInvalido());

            var sentido = request.Query["order"].ToString();
            if (string.IsNullOrEmpty(sentido))
                sentido = "asc";
            else if (!sentido.Equals("asc", StringComparison.OrdinalIgnoreCase) && !sentido.Equals("desc", StringComparison.OrdinalIgnoreCase))
                erros.Add(PaginacaoErros.SentidoInvalido());

            if (erros.Count > 0)
                return Result<PaginacaoDto>.Falha(erros.ToArray());

            var ordenacao = request.Query["sort"].ToString();
            return Result<PaginacaoDto>.Sucesso(new PaginacaoDto
            {
                Pagina = pagina!.Value,
                TamanhoPagina = tamanho!.Value,
                Ordenacao = string.IsNullOrWhiteSpace(ordenacao) ? null : ordenacao,
                Sentido = sentido.ToLowerInvariant()
            });
        }

        private static int? LerInteiro(string valor, int padrao, int minimo, int maximo)
        {
            if (string.IsNullOrEmpty(valor))
                return padrao;

            return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && numero >= minimo && numero <= maximo
                ? numero
                : null;
        }
    }
}
