using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class GmudRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.GMUD;

        private readonly IEtapaRulesService _service;

        public GmudRules(IEnumerable<IEtapaRulesService> services)
        {
            _service = services.OfType<EtapaGmudService>().First();
        }

        public Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            if (fluxoDeploy.Gmud is null)
                throw new NotFoundException("Erro ao finalizar etapa pois não existe uma GMUD vinculada ou uma justificativa de não informar uma GMUD");

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxoDeploy));
            return Task.CompletedTask;
        }
    }
}
