using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class AmbienteDevRules : AmbienteRulesBase, IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.DEV;

        private readonly IEtapaRulesService _service;

        public AmbienteDevRules(
            IDeployRepository deployRepository,
            IConfiguracaoSistemaRepository configuracaoSistemaRepository,
            IEnumerable<IEtapaRulesService> services)
            : base(deployRepository, configuracaoSistemaRepository)
        {
            _service = services.OfType<EtapaAmbienteDevService>().First();
        }

        public async Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            var deploys = await ObterDeploysDaBranchAsync(fluxoDeploy, "dev", cancellationToken);
            var configuracao = await ObterConfiguracaoAsync(fluxoDeploy, cancellationToken);

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(
                fluxoDeploy,
                Analises: AnalisesConsideradas(deploys),
                Config: configuracao));

            ConcluirDeploysEmExecucao(deploys);
        }
    }
}
