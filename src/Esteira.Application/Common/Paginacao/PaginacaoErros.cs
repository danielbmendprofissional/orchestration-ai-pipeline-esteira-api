using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Common.Paginacao
{
    public static class PaginacaoErros
    {
        public static Erro PaginaInvalida()
            => new("paginacao.pagina-invalida", "A página (X-Page) deve ser um número inteiro maior ou igual a 1.", TipoErro.Validation);

        public static Erro TamanhoInvalido()
            => new("paginacao.tamanho-invalido",
                $"O tamanho da página (X-Page-Size) deve ser um número inteiro entre 1 e {PaginacaoDto.TamanhoMaximo}.", TipoErro.Validation);

        public static Erro SentidoInvalido()
            => new("paginacao.sentido-invalido", "O sentido da ordenação (order) deve ser 'asc' ou 'desc'.", TipoErro.Validation);

        public static Erro OrdenacaoInvalida(string campo)
            => new("paginacao.ordenacao-invalida", $"Não é possível ordenar por '{campo}'.", TipoErro.Validation);
    }
}
