using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Interfaces;
using Esteira.Domain.FluxosDeploy.Services.Etapas;
using Esteira.Domain.FluxosDeploy.ValueObjects;
namespace Esteira.Application.FluxosDeploy.Commands.AlterarEtapaSeguinteFluxoDeploy.Rules
{
    public class EquipeTecnicaRules : IEtapaRules
    {
        public EtapaEnum Etapa => EtapaEnum.AET;

        private readonly IEtapaRulesService _service;
        private readonly IAprovadorTecnicoRepository _aprovadorTecnicoRepository;

        public EquipeTecnicaRules(
            IEnumerable<IEtapaRulesService> services,
            IAprovadorTecnicoRepository aprovadorTecnicoRepository)
        {
            _service = services.OfType<EtapaEquipeTecnicaService>().First();
            _aprovadorTecnicoRepository = aprovadorTecnicoRepository;
        }

        public async Task RegrasEtapaAsync(FluxoDeployEntity fluxoDeploy, CancellationToken cancellationToken = default)
        {
            var equipes = await _aprovadorTecnicoRepository.ObterEquipesTecnicasAsync(cancellationToken);

            var idsAprovadores = fluxoDeploy.AprovadoresTecnicos
                .Where(a => a.Aprovado)
                .Select(a => a.IdAprovadorTecnico)
                .ToList();

            var equipesAprovadas = (await _aprovadorTecnicoRepository.ObterPorIdsAsync(idsAprovadores, cancellationToken))
                .Select(a => a.Equipe);

            var equipesPendentes = equipes.Except(equipesAprovadas).ToList();

            if (equipesPendentes.Count > 0)
                throw new DomainException($"Não houve aprovação da(s) equipe(s): {string.Join(", ", equipesPendentes)}.");

            _service.ValidarConclusaoEtapa(new ValidacaoEtapaValueObject(fluxoDeploy));
        }
    }
}
