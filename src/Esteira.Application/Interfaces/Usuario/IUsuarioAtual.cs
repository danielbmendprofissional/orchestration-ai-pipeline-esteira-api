namespace Esteira.Application.Interfaces.Usuario
{
    public interface IUsuarioAtual
    {
        // `sub` do Keycloak: o identificador estável da pessoa (papéis por Aplicação apontam para ele).
        string Id { get; }
        string Usuario { get; }
        string Nome { get; }
        string Email { get; }
        IReadOnlyList<string> Perfis { get; }
        bool EhMor { get; }
    }
}
