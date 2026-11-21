namespace Esteira.Application.Interfaces.Aplicacoes
{
    // Registra quem entrou na Esteira (critério 16 da A1): grava ou atualiza a Pessoa.
    public interface IRegistradorDePessoa
    {
        Task RegistrarAsync(string sub, string nome, string email, CancellationToken cancellationToken = default);
    }
}
