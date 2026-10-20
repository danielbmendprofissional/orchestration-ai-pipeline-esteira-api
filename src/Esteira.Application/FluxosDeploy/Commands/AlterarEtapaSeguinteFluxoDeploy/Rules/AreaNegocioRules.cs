using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.Interfaces.Integracoes;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class AreaNegocioRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.AAN;

        private readonly IEtapaRulesService _service;
        private readonly IGerenciadorTarefas _gerenciadorTarefas;

        public AreaNegocioRules(IEnumerable<IEtapaRulesService> services, IGerenciadorTarefas gerenciadorTarefas)
        {
            _service = services.OfType<EtapaAreaNegocioService>().First();
            _gerenciadorTarefas = gerenciadorTarefas;
        }

        public async Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            if (fluxoDeploy.AprovadoresNegocio.Count == 0)
                throw new DomainException("É necessário ter pelo menos uma tarefa vinculada para aprovação da área de negócio!");

            var idsTarefas = fluxoDeploy.AprovadoresNegocio.Select(a => a.IdTarefa).ToList();
            var tarefas = await _gerenciadorTarefas.ObterTarefas(idsTarefas);

            var tarefasValidacao = idsTarefas
                .Select(id => new TarefaValueObject(id, tarefas.FirstOrDefault(t => t.Id == id)?.Status))
                .ToList();

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(
                fluxoDeploy,
                Tarefas: tarefasValidacao));
        }
    }
}
