namespace Esteira.Application.Aplicacoes
{
    // Configuração dos agentes do CI (X3). Lida da configuração a cada uso; não é segredo.
    public interface IOpcoesDosAgentes
    {
        // `Pipeline:TetoCustoPadraoUsd`; ausente, inválida ou fora da faixa 0.10-50.00 -> 2.00.
        decimal TetoCustoPadraoUsd { get; }
    }
}
