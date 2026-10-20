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
    public class AmbientePrdRules : AmbienteRulesBase, IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.PRD;

        private readonly IEtapaRulesService _service;

        public AmbientePrdRules(
            IDeployRepository deployRepository,
            IConfiguracaoSistemaRepository configuracaoSistemaRepository,
            IEnumerable<IEtapaRulesService> services)
            : base(deployRepository, configuracaoSistemaRepository)
        {
            _service = services.OfType<EtapaAmbientePrdService>().First();
        }

        public async Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            var deploys = await ObterDeploysDaBranchAsync(fluxoDeploy, "prd", cancellationToken);
            var configuracao = await ObterConfiguracaoAsync(fluxoDeploy, cancellationToken);

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(
                fluxoDeploy,
                Analises: AnalisesConsideradas(deploys),
                Config: configuracao));
        }
    }
}
