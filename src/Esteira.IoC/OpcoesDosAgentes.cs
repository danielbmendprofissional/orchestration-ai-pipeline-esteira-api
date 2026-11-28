using Esteira.Application.Aplicacoes;
using Microsoft.Extensions.Configuration;

namespace Esteira.IoC
{
    // Lê `Pipeline:TetoCustoPadraoUsd` a cada uso (configuração não secreta); a regra de fallback mora em TetoCustoPadrao.
    public sealed class OpcoesDosAgentes(IConfiguration configuracao) : IOpcoesDosAgentes
    {
        public decimal TetoCustoPadraoUsd => TetoCustoPadrao.Resolver(configuracao["Pipeline:TetoCustoPadraoUsd"]);
    }
}
