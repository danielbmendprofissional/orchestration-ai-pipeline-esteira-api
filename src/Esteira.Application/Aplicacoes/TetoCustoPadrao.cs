using System.Globalization;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Aplicacoes
{
    // Padrão da Esteira para o teto de custo dos agentes (X3): nunca erro, nunca zero, nunca "sem teto".
    public static class TetoCustoPadrao
    {
        public const decimal Valor = 2.00m;

        public static decimal Resolver(string? configurado)
        {
            // Sem separador de milhar: "1,5" é inválido, não 15.
            if (!decimal.TryParse(configurado, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
                || v < AplicacaoEntity.TetoCustoMinimoUsd || v > AplicacaoEntity.TetoCustoMaximoUsd)
                return Valor;

            return v;
        }
    }
}
