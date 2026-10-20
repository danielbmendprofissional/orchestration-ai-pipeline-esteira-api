using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Common.ValueObjects;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.ConfiguracoesSistema.Interfaces;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Deploys.Interfaces;
using Esteira.Domain.FluxosDeploy.Entities;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public abstract class AmbienteRulesBase
    {
        protected readonly IDeployRepository DeployRepository;
        protected readonly IConfiguracaoSistemaRepository ConfiguracaoSistemaRepository;

        protected AmbienteRulesBase(
            IDeployRepository deployRepository,
            IConfiguracaoSistemaRepository configuracaoSistemaRepository)
        {
            DeployRepository = deployRepository;
            ConfiguracaoSistemaRepository = configuracaoSistemaRepository;
        }

        protected async Task<IReadOnlyList<DeployEntity>> ObterDeploysDaBranchAsync(FluxoDeployEntity fluxoDeploy, string branch, CancellationToken cancellationToken)
        {
            var branchNormalizada = StringValueObject.Normalize(branch);
            var deploys = await DeployRepository.ObterPorFluxosAsync(new[] { fluxoDeploy.Id }, cancellationToken);

            return deploys
                .Where(d => d.Branch == branchNormalizada)
                .ToList();
        }

        protected static IEnumerable<AnaliseEntity> AnalisesConsideradas(IEnumerable<DeployEntity> deploys)
            => deploys
                .SelectMany(d => d.Analises)
                .Where(a => AnaliseEntity.StatusConsiderados.Contains(a.Status))
                .ToList();

        protected async Task<ConfiguracaoSistemaEntity> ObterConfiguracaoAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken)
            => await ConfiguracaoSistemaRepository.GetBySistemaAsync(fluxoDeploy.Sistema, cancellationToken)
               ?? throw new DomainException("Configuração do sistema não encontrada");

        protected static void ConcluirDeploysEmExecucao(IEnumerable<DeployEntity> deploys)
        {
            foreach (var deploy in deploys.Where(d => d.Status == StatusEnum.EXECUTANDO))
                deploy.Concluir();
        }
    }
}
