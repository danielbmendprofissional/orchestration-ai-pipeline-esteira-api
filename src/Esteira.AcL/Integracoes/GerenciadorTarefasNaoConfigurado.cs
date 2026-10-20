using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Exceptions;

namespace Esteira.AcL.Integracoes
{
    public sealed class GerenciadorTarefasNaoConfigurado : IGerenciadorTarefas
    {
        public Task<List<TarefaDto>> ObterTarefas(List<int> idTarefas)
            => throw new IntegrationException("Nenhum gerenciador de tarefas está configurado.", 501);
    }
}
