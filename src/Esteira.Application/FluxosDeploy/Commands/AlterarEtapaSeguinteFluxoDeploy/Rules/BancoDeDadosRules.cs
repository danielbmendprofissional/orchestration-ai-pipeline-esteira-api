using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class BancoDeDadosRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.BD;

        private readonly IEtapaRulesService _service;

        public BancoDeDadosRules(IEnumerable<IEtapaRulesService> services)
        {
            _service = services.OfType<EtapaBancoDeDadosService>().First();
        }

        public Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            if (fluxoDeploy.Bds.Count == 0)
                throw new DomainException("É necessário ter pelo menos um CRS associado para finalização da etapa!");

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxoDeploy));
            return Task.CompletedTask;
        }
    }
}
