using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Interfaces.Auditoria
{
    // Trilha de auditoria do cadastro (A1, critério 14). Registrar só enfileira o RegistroAuditoria no
    // mesmo contexto de escrita: ele sai no mesmo SaveChanges/transação da alteração e desfaz junto no rollback.
    public interface IAuditoria
    {
        // Quem = IUsuarioAtual.Id (sub); quando = agora em UTC. `antes`/`depois` são serializados em JSON
        // (null = sem estado, como no antes de uma criação ou no depois de uma remoção).
        void Registrar(string entidade, Guid entidadePublicId, AcaoAuditoria acao, object? antes, object? depois);
    }
}
