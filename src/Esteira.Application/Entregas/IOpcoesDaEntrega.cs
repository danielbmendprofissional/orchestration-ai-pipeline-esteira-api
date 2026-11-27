namespace Esteira.Application.Entregas
{
    // Flag `Features:E1b` (só `true` liga; ausente = desligado), lida da configuração a cada uso.
    public interface IOpcoesDaEntrega
    {
        bool E1bLigada { get; }
    }
}
