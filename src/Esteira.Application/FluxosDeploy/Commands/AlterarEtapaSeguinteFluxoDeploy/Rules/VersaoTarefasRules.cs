using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class VersaoTarefasRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.VERSAO;

        private readonly IEtapaRulesService _service;

        public VersaoTarefasRules(IEnumerable<IEtapaRulesService> services)
        {
            _service = services.OfType<EtapaVersaoTarefasService>().First();
        }

        public Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            if (fluxoDeploy.VersaoTarefas is null)
                throw new DomainException($"Versão das tarefas não cadastrada para fluxo de deploy: '{fluxoDeploy.Id}'");

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxoDeploy));
            return Task.CompletedTask;
        }
    }
}
