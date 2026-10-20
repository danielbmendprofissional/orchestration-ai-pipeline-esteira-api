using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class AberturaRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.ABER;

        private readonly IEtapaRulesService _service;

        public AberturaRules(IEnumerable<IEtapaRulesService> services)
        {
            _service = services.OfType<EtapaAberturaService>().First();
        }

        public Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxoDeploy));
            return Task.CompletedTask;
        }
    }
}
