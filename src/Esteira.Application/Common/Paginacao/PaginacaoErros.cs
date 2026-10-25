using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Common.Paginacao
{
    public static class PaginacaoErros
    {
        public static Erro OrdenacaoInvalida(string campo)
            => new("paginacao.ordenacao-invalida", $"Não é possível ordenar por '{campo}'.", TipoErro.Validation);
    }
}
