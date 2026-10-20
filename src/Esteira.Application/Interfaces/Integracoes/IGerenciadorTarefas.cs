using Esteira.Application.DTOs.Response;

namespace Esteira.Application.Interfaces.Integracoes
{
    // Porta para o gerenciador de tarefas, usada para conferir se as tarefas vinculadas à aprovação de negócio estão aprovadas.
    // Sem ferramenta definida: a ideia é um adaptador para GitLab (issues/specs vinculadas ao fluxo).
    public interface IGerenciadorTarefas
    {
        Task<List<TarefaDto>> ObterTarefas(List<int> idTarefas);
    }
}
